using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Network.Apt;

/// <summary>
/// Orchestration de la mise à jour applicative au-dessus d'APT.
///
/// Le workflow reste en deux temps — télécharger puis installer — pour que l'opérateur
/// puisse préparer la mise à jour sans couper le nœud, et déclencher l'installation au
/// moment qui lui convient. Le téléchargement remplit le cache d'apt ; l'installation
/// est alors immédiate et ne dépend plus du réseau.
/// </summary>
public class AptApplicationUpdateWorkflowService : IApplicationUpdateWorkflowService
{
    private readonly IApplicationUpdateService _updateService;
    private readonly IAptCommandRunner _runner;
    private readonly ILogger<AptApplicationUpdateWorkflowService> _logger;
    private readonly AptUpdateOptions _options;

    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _stateLock = new();
    private WorkflowState _state = WorkflowState.CreateInitial();

    /// <summary>Répertoire de cache d'apt, où atterrissent les paquets téléchargés.</summary>
    internal const string AptCacheDirectory = "/var/cache/apt/archives";

    public AptApplicationUpdateWorkflowService(
        IApplicationUpdateService updateService,
        IAptCommandRunner runner,
        ILogger<AptApplicationUpdateWorkflowService> logger,
        IOptions<AptUpdateOptions> options)
    {
        _updateService = updateService;
        _runner = runner;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> GetStatusAsync(
        ApplicationUpdateChannel? channel = null,
        CancellationToken cancellationToken = default)
    {
        var status = await _updateService.GetStatusAsync(channel, cancellationToken);
        return status.Map(BuildDto);
    }

    public async Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> DownloadLatestAsync(
        ApplicationUpdateChannel? channel = null,
        CancellationToken cancellationToken = default)
    {
        if (!await _operationLock.WaitAsync(0, cancellationToken))
        {
            return Error.Validation(
                    "APPLICATION_UPDATE_BUSY",
                    "Une opération de mise à jour est déjà en cours.")
                .ToFailure<ApplicationUpdateWorkflowStatusDto>();
        }

        try
        {
            var statusResult = await _updateService.GetStatusAsync(channel, cancellationToken);
            if (statusResult.IsFail)
                return statusResult.Map(_ => default(ApplicationUpdateWorkflowStatusDto)!);

            var status = statusResult.Match(s => s, _ => throw new InvalidOperationException("Succès attendu."));

            if (!status.IsUpdateAvailable || status.LatestRelease is null)
            {
                UpdateState(s => s with
                {
                    OperationState = ApplicationUpdateOperationState.Idle,
                    LastOperationMessage = "Aucune mise à jour à télécharger.",
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                return Validation<Error, ApplicationUpdateWorkflowStatusDto>.Success(BuildDto(status));
            }

            UpdateState(s => s with
            {
                OperationState = ApplicationUpdateOperationState.Downloading,
                DownloadedPackage = null,
                LastOperationMessage = null,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            var download = await _runner.RunAsync(
                "apt-get",
                ["install", "--download-only", "--yes", $"{_options.PackageName}={status.LatestRelease.Version}"],
                cancellationToken);

            if (!download.Succeeded)
            {
                UpdateState(s => s with
                {
                    OperationState = ApplicationUpdateOperationState.Failed,
                    LastOperationMessage = download.ErrorMessage,
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                return Error.Validation(
                        "APPLICATION_UPDATE_DOWNLOAD_FAILED",
                        $"Échec du téléchargement : {download.ErrorMessage}")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            var package = LocateCachedPackage(status.LatestRelease.Version);

            UpdateState(s => s with
            {
                OperationState = ApplicationUpdateOperationState.Downloaded,
                DownloadProgressPercent = 100,
                DownloadedPackage = package,
                LastOperationMessage = $"Version {status.LatestRelease.Version} téléchargée et prête à installer.",
                UpdatedAt = DateTimeOffset.UtcNow
            });

            _logger.LogInformation(
                "Paquet {Package} {Version} téléchargé dans le cache APT",
                _options.PackageName,
                status.LatestRelease.Version);

            return Validation<Error, ApplicationUpdateWorkflowStatusDto>.Success(BuildDto(status));
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> RequestInstallAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await _operationLock.WaitAsync(0, cancellationToken))
        {
            return Error.Validation(
                    "APPLICATION_UPDATE_BUSY",
                    "Une opération de mise à jour est déjà en cours.")
                .ToFailure<ApplicationUpdateWorkflowStatusDto>();
        }

        try
        {
            var state = GetState();
            if (state.DownloadedPackage is null)
            {
                return Error.Validation(
                        "APPLICATION_UPDATE_NOT_DOWNLOADED",
                        "Aucun paquet n'a été téléchargé : lancez d'abord le téléchargement.")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            var version = state.DownloadedPackage.Version;

            // L'installation est déléguée à une unité systemd transitoire, et non lancée
            // dans le processus courant : le postinst du paquet redémarre le service, ce
            // qui tuerait apt en pleine transaction dpkg et laisserait le paquet à moitié
            // configuré. L'unité transitoire survit à l'arrêt du service.
            var install = await _runner.RunAsync(
                "systemd-run",
                [
                    "--unit=svxlinkmanagerv2-update",
                    "--description=Mise à jour de SvxlinkManagerV2",
                    "--collect",
                    "/bin/sh", "-c",
                    $"sleep 2; DEBIAN_FRONTEND=noninteractive apt-get install --yes {_options.PackageName}={version}"
                ],
                cancellationToken);

            if (!install.Succeeded)
            {
                UpdateState(s => s with
                {
                    OperationState = ApplicationUpdateOperationState.Failed,
                    LastOperationMessage = install.ErrorMessage,
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                return Error.Validation(
                        "APPLICATION_UPDATE_INSTALL_FAILED",
                        $"Impossible de lancer l'installation : {install.ErrorMessage}")
                    .ToFailure<ApplicationUpdateWorkflowStatusDto>();
            }

            UpdateState(s => s with
            {
                OperationState = ApplicationUpdateOperationState.InstallRequested,
                LastOperationMessage =
                    $"Installation de la version {version} lancée. Le service va redémarrer, "
                    + "l'interface sera brièvement indisponible.",
                UpdatedAt = DateTimeOffset.UtcNow
            });

            _logger.LogInformation("Installation de {Package} {Version} déléguée à systemd-run",
                _options.PackageName, version);

            var status = await _updateService.GetStatusAsync(null, cancellationToken);
            return status.Match(
                Succ: s => Validation<Error, ApplicationUpdateWorkflowStatusDto>.Success(BuildDto(s)),
                Fail: errors => Validation<Error, ApplicationUpdateWorkflowStatusDto>.Fail(errors));
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Retrouve le paquet déposé par apt dans son cache. Le nom de fichier encode le
    /// tilde des préversions (« %7e »), d'où la recherche par motif plutôt que par nom
    /// reconstruit. L'absence du fichier n'est pas une erreur : le téléchargement a
    /// réussi, seule la taille affichée manquera.
    /// </summary>
    internal ApplicationDownloadedPackageInfo LocateCachedPackage(string version)
    {
        try
        {
            if (Directory.Exists(AptCacheDirectory))
            {
                var match = Directory
                    .EnumerateFiles(AptCacheDirectory, $"{_options.PackageName}_*.deb")
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (match is not null)
                {
                    return new ApplicationDownloadedPackageInfo(
                        Version: version,
                        FileName: match.Name,
                        FilePath: match.FullName,
                        FileSizeBytes: match.Length,
                        DownloadedAt: DateTimeOffset.UtcNow,
                        SourceUrl: null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Impossible d'inspecter le cache APT {Directory}", AptCacheDirectory);
        }

        return new ApplicationDownloadedPackageInfo(
            Version: version,
            FileName: $"{_options.PackageName}_{version}.deb",
            FilePath: AptCacheDirectory,
            FileSizeBytes: 0,
            DownloadedAt: DateTimeOffset.UtcNow,
            SourceUrl: null);
    }

    private ApplicationUpdateWorkflowStatusDto BuildDto(ApplicationUpdateStatusDto updateStatus)
    {
        var state = GetState();

        var canDownload = !state.IsBusy && updateStatus.IsUpdateAvailable;

        // L'installation n'est proposée que si le paquet en cache correspond encore à la
        // version proposée : changer de canal entre les deux étapes rendrait l'installation
        // d'une version périmée silencieusement possible.
        var canRequestInstall = !state.IsBusy
            && state.DownloadedPackage is not null
            && state.OperationState == ApplicationUpdateOperationState.Downloaded
            && string.Equals(
                state.DownloadedPackage.Version,
                updateStatus.LatestRelease?.Version,
                StringComparison.OrdinalIgnoreCase);

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
