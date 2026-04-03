using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Network;

/// <summary>
/// Orchestration locale du téléchargement et de la demande d'installation des mises à jour.
/// </summary>
public class ApplicationUpdateWorkflowService : IApplicationUpdateWorkflowService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApplicationUpdateWorkflowService> _logger;
    private readonly ApplicationUpdateOptions _options;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _stateLock = new();
    private WorkflowState _state = WorkflowState.CreateInitial();

    public ApplicationUpdateWorkflowService(
        IServiceScopeFactory serviceScopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ApplicationUpdateWorkflowService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = configuration.GetSection(ApplicationUpdateOptions.SectionName).Get<ApplicationUpdateOptions>()
            ?? new ApplicationUpdateOptions();
    }

    public async Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> GetStatusAsync(
        ApplicationUpdateChannel? channel = null,
        CancellationToken cancellationToken = default)
    {
        var updateStatusResult = await GetUpdateStatusAsync(channel, cancellationToken);
        if (updateStatusResult.IsFail)
            return updateStatusResult.Map(_ => default(ApplicationUpdateWorkflowStatusDto)!);

        var updateStatus = updateStatusResult.Match(
            Succ: status => status,
            Fail: _ => throw new InvalidOperationException("Un succès était attendu."));

        return Validation<Error, ApplicationUpdateWorkflowStatusDto>.Success(BuildDto(updateStatus));
    }

    public async Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> DownloadLatestAsync(
        ApplicationUpdateChannel? channel = null,
        CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);

        try
        {
            var updateStatusResult = await GetUpdateStatusAsync(channel, cancellationToken);
            if (updateStatusResult.IsFail)
            {
                UpdateState(state => state with
                {
                    OperationState = ApplicationUpdateOperationState.Failed,
                    DownloadProgressPercent = null,
                    LastOperationMessage = "Impossible de déterminer la release à télécharger.",
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                return updateStatusResult.Map(_ => default(ApplicationUpdateWorkflowStatusDto)!);
            }

            var updateStatus = updateStatusResult.Match(
                Succ: status => status,
                Fail: _ => throw new InvalidOperationException("Un succès était attendu."));

            if (!updateStatus.IsUpdateAvailable || updateStatus.LatestRelease is null)
            {
                return Error.Validation(
                        "APPLICATION_UPDATE_NOT_AVAILABLE",
                        "Aucune mise à jour disponible à télécharger sur ce canal.")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            if (string.IsNullOrWhiteSpace(updateStatus.LatestRelease.PackageUrl)
                || string.IsNullOrWhiteSpace(updateStatus.LatestRelease.PackageName))
            {
                return Error.Validation(
                        "APPLICATION_UPDATE_PACKAGE_MISSING",
                        "Aucun paquet .deb n'est disponible pour cette release.")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

                    if (string.IsNullOrWhiteSpace(updateStatus.LatestRelease.ChecksumUrl))
                    {
                    return Error.Validation(
                        "APPLICATION_UPDATE_CHECKSUM_MISSING",
                        "Aucun fichier checksum (.sha256) n'est disponible pour cette release.")
                        .ToFailure<ApplicationUpdateWorkflowStatusDto>();
                    }

            var stagingDirectory = ResolveStagingDirectory(_options.StagingDirectory);
            Directory.CreateDirectory(stagingDirectory);

            var packagePath = Path.Combine(stagingDirectory, updateStatus.LatestRelease.PackageName);
            var temporaryPath = packagePath + ".download";

            UpdateState(state => state with
            {
                OperationState = ApplicationUpdateOperationState.Downloading,
                DownloadProgressPercent = 0,
                LastOperationMessage = $"Téléchargement de {updateStatus.LatestRelease.PackageName} en cours...",
                UpdatedAt = DateTimeOffset.UtcNow
            });

            var client = CreateGitHubHttpClient();
            var downloadUrl = BuildGitHubAssetDownloadUrl(updateStatus.LatestRelease.PackageUrl!, updateStatus.LatestRelease.PackageAssetId);
            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            downloadRequest.Headers.Accept.Clear();
            downloadRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/octet-stream"));
            using var response = await client.SendAsync(
                downloadRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Error.Validation(
                        "APPLICATION_UPDATE_DOWNLOAD_HTTP_ERROR",
                        $"Le téléchargement a échoué (HTTP {(int)response.StatusCode}).")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            var totalBytes = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;

            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                totalRead += read;

                if (totalBytes is > 0)
                {
                    var progress = (int)Math.Clamp(Math.Round((double)totalRead * 100 / totalBytes.Value), 0, 100);
                    UpdateState(state => state with
                    {
                        DownloadProgressPercent = progress,
                        LastOperationMessage = $"Téléchargement en cours... {progress}%",
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
            }

            await target.FlushAsync(cancellationToken);

            var checksumContent = await DownloadChecksumFileAsync(client, updateStatus.LatestRelease.ChecksumUrl, updateStatus.LatestRelease.ChecksumAssetId, cancellationToken);
            var expectedChecksum = ExtractSha256FromChecksumContent(checksumContent, updateStatus.LatestRelease.PackageName);
            if (string.IsNullOrWhiteSpace(expectedChecksum))
            {
                return Error.Validation(
                        "APPLICATION_UPDATE_CHECKSUM_INVALID",
                        "Le fichier checksum téléchargé est invalide ou incomplet.")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            var actualChecksum = await ComputeSha256Async(temporaryPath, cancellationToken);
            if (!string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase))
            {
                return Error.Validation(
                        "APPLICATION_UPDATE_CHECKSUM_MISMATCH",
                        "Le checksum SHA-256 du paquet téléchargé ne correspond pas à celui publié avec la release.")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            File.Move(temporaryPath, packagePath);

            var downloadedPackage = new ApplicationDownloadedPackageInfo(
                Version: updateStatus.LatestRelease.Version,
                FileName: updateStatus.LatestRelease.PackageName,
                FilePath: packagePath,
                FileSizeBytes: new FileInfo(packagePath).Length,
                DownloadedAt: DateTimeOffset.UtcNow,
                SourceUrl: updateStatus.LatestRelease.PackageUrl);

            UpdateState(state => state with
            {
                OperationState = ApplicationUpdateOperationState.Downloaded,
                DownloadProgressPercent = 100,
                DownloadedPackage = downloadedPackage,
                LastOperationMessage = $"Paquet téléchargé et vérifié (SHA-256) dans {packagePath}.",
                UpdatedAt = DateTimeOffset.UtcNow
            });

            return Validation<Error, ApplicationUpdateWorkflowStatusDto>.Success(BuildDto(updateStatus));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du téléchargement de la mise à jour applicative");
            UpdateState(state => state with
            {
                OperationState = ApplicationUpdateOperationState.Failed,
                DownloadProgressPercent = null,
                LastOperationMessage = $"Échec du téléchargement : {ex.Message}",
                UpdatedAt = DateTimeOffset.UtcNow
            });

            return Error.Validation(
                    "APPLICATION_UPDATE_DOWNLOAD_ERROR",
                    $"Erreur lors du téléchargement de la mise à jour : {ex.Message}")
                .ToFailure<ApplicationUpdateWorkflowStatusDto>();
        }
        finally
        {
            await CleanupTemporaryDownloadsAsync();
            _operationLock.Release();
        }
    }

    public async Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> RequestInstallAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);

        try
        {
            var state = GetState();
            if (state.DownloadedPackage is null || !File.Exists(state.DownloadedPackage.FilePath))
            {
                return Error.Validation(
                        "APPLICATION_UPDATE_PACKAGE_NOT_DOWNLOADED",
                        "Aucun paquet téléchargé n'est disponible pour l'installation.")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            if (string.IsNullOrWhiteSpace(_options.InstallCommand))
            {
                UpdateState(current => current with
                {
                    OperationState = ApplicationUpdateOperationState.Failed,
                    LastOperationMessage = "Le helper d'installation n'est pas configuré.",
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                return Error.Validation(
                        "APPLICATION_UPDATE_INSTALLER_NOT_CONFIGURED",
                        "Aucun helper d'installation n'est configuré. Configure ApplicationUpdate:InstallCommand pour activer cette action.")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            var arguments = ExpandInstallArguments(_options.InstallArguments, state.DownloadedPackage);
            UpdateState(current => current with
            {
                OperationState = ApplicationUpdateOperationState.InstallRequested,
                LastOperationMessage = "Demande d'installation en cours d'exécution...",
                UpdatedAt = DateTimeOffset.UtcNow
            });

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.InstallCommand,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            var waitForExitTask = process.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.InstallCommandTimeoutSeconds)), cancellationToken);
            var completed = await Task.WhenAny(waitForExitTask, timeoutTask);

            if (completed == timeoutTask && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Rien à faire ici, le process a peut-être déjà quitté.
                }

                return Error.Validation(
                        "APPLICATION_UPDATE_INSTALL_TIMEOUT",
                        "La demande d'installation a dépassé le délai autorisé.")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                UpdateState(current => current with
                {
                    OperationState = ApplicationUpdateOperationState.Failed,
                    LastOperationMessage = string.IsNullOrWhiteSpace(stderr)
                        ? $"Le helper d'installation a échoué avec le code {process.ExitCode}."
                        : stderr.Trim(),
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                return Error.Validation(
                        "APPLICATION_UPDATE_INSTALL_ERROR",
                        string.IsNullOrWhiteSpace(stderr)
                            ? $"Le helper d'installation a échoué avec le code {process.ExitCode}."
                            : stderr.Trim())
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            UpdateState(current => current with
            {
                OperationState = ApplicationUpdateOperationState.InstallRequested,
                LastOperationMessage = string.IsNullOrWhiteSpace(stdout)
                    ? "La demande d'installation a été transmise avec succès."
                    : stdout.Trim(),
                UpdatedAt = DateTimeOffset.UtcNow
            });

            var statusResult = await GetStatusAsync(null, cancellationToken);
            return statusResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la demande d'installation de la mise à jour");
            UpdateState(current => current with
            {
                OperationState = ApplicationUpdateOperationState.Failed,
                LastOperationMessage = $"Échec de la demande d'installation : {ex.Message}",
                UpdatedAt = DateTimeOffset.UtcNow
            });

            return Error.Validation(
                    "APPLICATION_UPDATE_INSTALL_REQUEST_ERROR",
                    $"Erreur lors de la demande d'installation : {ex.Message}")
                .ToFailure<ApplicationUpdateWorkflowStatusDto>();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    internal static string ResolveStagingDirectory(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }

    internal static string ExpandInstallArguments(string? template, ApplicationDownloadedPackageInfo package)
    {
        var effectiveTemplate = string.IsNullOrWhiteSpace(template) ? "{packagePath}" : template;
        var packageDirectory = GetPackageDirectory(package.FilePath);

        return effectiveTemplate
            .Replace("{packagePath}", QuoteArgument(package.FilePath), StringComparison.Ordinal)
            .Replace("{packageName}", QuoteArgument(package.FileName), StringComparison.Ordinal)
            .Replace("{packageDirectory}", QuoteArgument(packageDirectory), StringComparison.Ordinal)
            .Replace("{version}", QuoteArgument(package.Version), StringComparison.Ordinal);
    }

    internal static string QuoteArgument(string value)
        => value.Contains(' ') ? $"\"{value}\"" : value;

    internal static string GetPackageDirectory(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            return string.Empty;

        var lastSeparatorIndex = Math.Max(packagePath.LastIndexOf('/'), packagePath.LastIndexOf('\\'));
        return lastSeparatorIndex <= 0 ? string.Empty : packagePath[..lastSeparatorIndex];
    }

    internal static string? ExtractSha256FromChecksumContent(string content, string packageName)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var lines = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
            return null;

        var matchingLine = lines.FirstOrDefault(line =>
            line.Contains(packageName, StringComparison.OrdinalIgnoreCase));

        var targetLine = matchingLine ?? lines[0];
        var firstToken = targetLine
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstToken) || firstToken.Length != 64)
            return null;

        return firstToken.ToLowerInvariant();
    }

    internal static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private HttpClient CreateGitHubHttpClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.github.com/");
        if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("SvxlinkManagerV2", "1.0"));
        GitHubReleaseUpdateService.ApplyGitHubAuthorizationHeader(client, _options.GitHubToken);
        return client;
    }

    private string BuildGitHubAssetDownloadUrl(string browserDownloadUrl, long? assetId)
    {
        if (assetId.HasValue
            && !string.IsNullOrWhiteSpace(_options.Owner)
            && !string.IsNullOrWhiteSpace(_options.Repository))
        {
            return $"https://api.github.com/repos/{_options.Owner}/{_options.Repository}/releases/assets/{assetId.Value}";
        }

        return browserDownloadUrl;
    }

    private static async Task<string> DownloadChecksumFileAsync(
        HttpClient client,
        string? checksumUrl,
        long? assetId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(checksumUrl))
            return string.Empty;

        using var request = new HttpRequestMessage(HttpMethod.Get, checksumUrl);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/octet-stream"));
        using var checksumResponse = await client.SendAsync(request, cancellationToken);
        checksumResponse.EnsureSuccessStatusCode();
        return await checksumResponse.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<Validation<Error, ApplicationUpdateStatusDto>> GetUpdateStatusAsync(
        ApplicationUpdateChannel? channel,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var updateService = scope.ServiceProvider.GetRequiredService<IApplicationUpdateService>();
        return await updateService.GetStatusAsync(channel, cancellationToken);
    }

    private ApplicationUpdateWorkflowStatusDto BuildDto(ApplicationUpdateStatusDto updateStatus)
    {
        var state = GetState();
        var canDownload = !state.IsBusy
            && updateStatus.IsUpdateAvailable
            && !string.IsNullOrWhiteSpace(updateStatus.LatestRelease?.PackageUrl);
        var canRequestInstall = !state.IsBusy && state.DownloadedPackage is not null;

        return new ApplicationUpdateWorkflowStatusDto(
            UpdateStatus: updateStatus,
            OperationState: state.OperationState,
            DownloadProgressPercent: state.DownloadProgressPercent,
            DownloadedPackage: state.DownloadedPackage,
            IsBusy: state.IsBusy,
            CanDownload: canDownload,
            CanRequestInstall: canRequestInstall,
            LastOperationMessage: state.LastOperationMessage,
            UpdatedAt: state.UpdatedAt);
    }

    private WorkflowState GetState()
    {
        lock (_stateLock)
        {
            return _state;
        }
    }

    private void UpdateState(Func<WorkflowState, WorkflowState> updater)
    {
        lock (_stateLock)
        {
            _state = updater(_state);
        }
    }

    private async Task CleanupTemporaryDownloadsAsync()
    {
        try
        {
            var stagingDirectory = ResolveStagingDirectory(_options.StagingDirectory);
            if (!Directory.Exists(stagingDirectory))
                return;

            foreach (var file in Directory.EnumerateFiles(stagingDirectory, "*.download"))
            {
                await Task.Run(() => File.Delete(file));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Impossible de nettoyer les fichiers temporaires de téléchargement");
        }
    }

    private sealed record WorkflowState(
        ApplicationUpdateOperationState OperationState,
        int? DownloadProgressPercent,
        ApplicationDownloadedPackageInfo? DownloadedPackage,
        string? LastOperationMessage,
        DateTimeOffset UpdatedAt)
    {
        public bool IsBusy => OperationState == ApplicationUpdateOperationState.Downloading;

        public static WorkflowState CreateInitial() => new(
            OperationState: ApplicationUpdateOperationState.Idle,
            DownloadProgressPercent: null,
            DownloadedPackage: null,
            LastOperationMessage: null,
            UpdatedAt: DateTimeOffset.UtcNow);
    }
}