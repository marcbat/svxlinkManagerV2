using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Network;

/// <summary>
/// Consultation des mises à jour applicatives via l'API GitHub Releases.
/// </summary>
public class GitHubReleaseUpdateService : IApplicationUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubReleaseUpdateService> _logger;
    private readonly ApplicationUpdateOptions _options;

    public GitHubReleaseUpdateService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GitHubReleaseUpdateService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = configuration.GetSection(ApplicationUpdateOptions.SectionName).Get<ApplicationUpdateOptions>()
            ?? new ApplicationUpdateOptions();

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri("https://api.github.com/");
        }

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SvxlinkManagerV2", "1.0"));
        }

        if (!_httpClient.DefaultRequestHeaders.Accept.Any())
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        ApplyGitHubAuthorizationHeader(_httpClient, _options.GitHubToken);
    }

    public async Task<Validation<Error, ApplicationUpdateStatusDto>> GetStatusAsync(
        ApplicationUpdateChannel? channel = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveChannel = channel ?? _options.Channel;
        var currentVersion = GetCurrentVersion();

        if (!_options.Enabled)
        {
            return Validation<Error, ApplicationUpdateStatusDto>.Success(
                new ApplicationUpdateStatusDto(
                    CurrentVersion: currentVersion,
                    Channel: effectiveChannel,
                    IsConfigured: false,
                    IsUpdateAvailable: false,
                    LatestRelease: null,
                    Message: "La vérification des mises à jour est désactivée."));
        }

        if (string.IsNullOrWhiteSpace(_options.Owner) || string.IsNullOrWhiteSpace(_options.Repository))
        {
            return Validation<Error, ApplicationUpdateStatusDto>.Success(
                new ApplicationUpdateStatusDto(
                    CurrentVersion: currentVersion,
                    Channel: effectiveChannel,
                    IsConfigured: false,
                    IsUpdateAvailable: false,
                    LatestRelease: null,
                    Message: "Le dépôt GitHub de mise à jour n'est pas configuré."));
        }

        try
        {
            var requestUri = $"repos/{_options.Owner}/{_options.Repository}/releases?per_page=100";
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "GitHub Releases a répondu avec le statut {StatusCode}: {Body}",
                    (int)response.StatusCode,
                    body);

                return Error.Validation(
                    "APPLICATION_UPDATE_HTTP_ERROR",
                    BuildGitHubApiErrorMessage((int)response.StatusCode, _options.GitHubToken))
                    .ToFailure<ApplicationUpdateStatusDto>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, JsonOptions, cancellationToken)
                ?? [];

            var latestRelease = SelectLatestRelease(releases, effectiveChannel, _options.PackagePattern);

            if (latestRelease is null)
            {
                return Validation<Error, ApplicationUpdateStatusDto>.Success(
                    new ApplicationUpdateStatusDto(
                        CurrentVersion: currentVersion,
                        Channel: effectiveChannel,
                        IsConfigured: true,
                        IsUpdateAvailable: false,
                        LatestRelease: null,
                        Message: "Aucune release compatible n'a été trouvée sur ce canal."));
            }

            var latestVersion = NormalizeVersion(latestRelease.TagName) ?? latestRelease.TagName;
            var currentComparable = TryParseComparableVersion(currentVersion, out var parsedCurrent);
            var latestComparable = TryParseComparableVersion(latestVersion, out var parsedLatest);

            var updateAvailable = currentComparable && latestComparable
                ? Compare(parsedLatest, parsedCurrent) > 0
                : !string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase);

            var package = SelectPackageAsset(latestRelease.Assets, _options.PackagePattern);
            var checksum = SelectChecksumAsset(latestRelease.Assets, package?.Name);
            var status = new ApplicationUpdateStatusDto(
                CurrentVersion: currentVersion,
                Channel: effectiveChannel,
                IsConfigured: true,
                IsUpdateAvailable: updateAvailable,
                LatestRelease: new ApplicationReleaseInfo(
                    Version: latestVersion,
                    Tag: latestRelease.TagName,
                    Name: latestRelease.Name ?? latestRelease.TagName,
                    PublishedAt: latestRelease.PublishedAt ?? latestRelease.CreatedAt ?? DateTimeOffset.MinValue,
                    IsPrerelease: latestRelease.Prerelease,
                    ReleaseNotesUrl: latestRelease.HtmlUrl,
                    ChecksumUrl: checksum?.BrowserDownloadUrl,
                    PackageUrl: package?.BrowserDownloadUrl,
                    PackageName: package?.Name,
                    PackageAssetId: package?.Id,
                    ChecksumAssetId: checksum?.Id),
                Message: updateAvailable
                    ? "Une nouvelle version est disponible sur le canal sélectionné."
                    : "Vous disposez déjà de la dernière version connue pour ce canal.");

            return Validation<Error, ApplicationUpdateStatusDto>.Success(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la consultation des mises à jour GitHub");
            return Error.Validation("APPLICATION_UPDATE_ERROR", $"Erreur lors de la consultation des mises à jour : {ex.Message}")
                .ToFailure<ApplicationUpdateStatusDto>();
        }
    }

    internal static GitHubRelease? SelectLatestRelease(
        IEnumerable<GitHubRelease> releases,
        ApplicationUpdateChannel channel,
        string packagePattern)
    {
        return releases
            .Where(release => !release.Draft)
            .Where(release => MatchesChannel(release, channel))
            .Where(release => SelectPackageAsset(release.Assets, packagePattern) is not null)
            .OrderByDescending(release => NormalizeVersion(release.TagName), ComparableVersionComparer.Instance)
            .ThenByDescending(release => release.PublishedAt ?? release.CreatedAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    internal static GitHubReleaseAsset? SelectPackageAsset(
        IEnumerable<GitHubReleaseAsset>? assets,
        string packagePattern)
    {
        if (assets is null)
            return null;

        var suffix = packagePattern.Trim().TrimStart('*');
        return assets.FirstOrDefault(asset =>
            !string.IsNullOrWhiteSpace(asset.Name)
            && asset.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    internal static GitHubReleaseAsset? SelectChecksumAsset(
        IEnumerable<GitHubReleaseAsset>? assets,
        string? packageName)
    {
        if (assets is null)
            return null;

        if (!string.IsNullOrWhiteSpace(packageName))
        {
            var expectedChecksumName = Path.GetFileNameWithoutExtension(packageName) + ".sha256";
            var matchingAsset = assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, expectedChecksumName, StringComparison.OrdinalIgnoreCase));
            if (matchingAsset is not null)
                return matchingAsset;
        }

        return assets.FirstOrDefault(asset =>
            !string.IsNullOrWhiteSpace(asset.Name)
            && asset.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool MatchesChannel(GitHubRelease release, ApplicationUpdateChannel channel)
    {
        return channel switch
        {
            ApplicationUpdateChannel.Stable => !release.Prerelease,
            ApplicationUpdateChannel.Beta => release.Prerelease && HasPreReleaseLabel(release.TagName, "beta"),
            ApplicationUpdateChannel.Development => release.Prerelease && HasPreReleaseLabel(release.TagName, "alpha"),
            _ => false
        };
    }

    internal static bool HasPreReleaseLabel(string? tagName, string expectedLabel)
    {
        if (!TryParseComparableVersion(tagName, out var version) || string.IsNullOrWhiteSpace(version.PreRelease))
            return false;

        var label = version.PreRelease.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];
        return string.Equals(label, expectedLabel, StringComparison.OrdinalIgnoreCase);
    }

    internal static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return NormalizeVersion(informational) ?? informational;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    internal static void ApplyGitHubAuthorizationHeader(HttpClient httpClient, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
    }

    internal static string BuildGitHubApiErrorMessage(int statusCode, string? token)
    {
        if (statusCode == 401)
        {
            return "Impossible de récupérer les releases GitHub (HTTP 401 - Non autorisé). Le token GitHub est invalide ou a été révoqué. Génère un nouveau token et configure ApplicationUpdate:GitHubToken.";
        }

        if (statusCode == 404 && string.IsNullOrWhiteSpace(token))
        {
            return "Impossible de récupérer les releases GitHub (HTTP 404). Si le dépôt est privé, configure ApplicationUpdate:GitHubToken.";
        }

        if (statusCode == 404 && !string.IsNullOrWhiteSpace(token))
        {
            return "Impossible de récupérer les releases GitHub (HTTP 404). Le token GitHub est peut-être révoqué ou invalide (GitHub retourne 404 pour les dépôts privés avec un token invalide). Génère un nouveau token.";
        }

        return $"Impossible de récupérer les releases GitHub (HTTP {statusCode}).";
    }

    internal static string? NormalizeVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[1..];

        var plusIndex = normalized.IndexOf('+');
        if (plusIndex >= 0)
            normalized = normalized[..plusIndex];

        normalized = normalized
            .Replace("~alpha.", "-alpha.", StringComparison.OrdinalIgnoreCase)
            .Replace("~beta.", "-beta.", StringComparison.OrdinalIgnoreCase)
            .Replace("~rc.", "-rc.", StringComparison.OrdinalIgnoreCase)
            .Replace("~hotfix.", "-hotfix.", StringComparison.OrdinalIgnoreCase)
            .Replace('~', '-');

        return normalized;
    }

    internal static bool TryParseComparableVersion(string? value, out ComparableVersion version)
    {
        version = default;

        var normalized = NormalizeVersion(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var parts = normalized.Split('-', 2, StringSplitOptions.TrimEntries);
        var core = parts[0].Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (core.Length != 3)
            return false;

        if (!int.TryParse(core[0], out var major)
            || !int.TryParse(core[1], out var minor)
            || !int.TryParse(core[2], out var patch))
        {
            return false;
        }

        version = new ComparableVersion(major, minor, patch, parts.Length > 1 ? parts[1] : null);
        return true;
    }

    internal static int Compare(ComparableVersion left, ComparableVersion right)
    {
        var major = left.Major.CompareTo(right.Major);
        if (major != 0)
            return major;

        var minor = left.Minor.CompareTo(right.Minor);
        if (minor != 0)
            return minor;

        var patch = left.Patch.CompareTo(right.Patch);
        if (patch != 0)
            return patch;

        if (string.IsNullOrWhiteSpace(left.PreRelease) && string.IsNullOrWhiteSpace(right.PreRelease))
            return 0;

        if (string.IsNullOrWhiteSpace(left.PreRelease))
            return 1;

        if (string.IsNullOrWhiteSpace(right.PreRelease))
            return -1;

        var leftIdentifiers = left.PreRelease.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var rightIdentifiers = right.PreRelease.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var count = Math.Max(leftIdentifiers.Length, rightIdentifiers.Length);

        for (var index = 0; index < count; index++)
        {
            if (index >= leftIdentifiers.Length)
                return -1;

            if (index >= rightIdentifiers.Length)
                return 1;

            var leftValue = leftIdentifiers[index];
            var rightValue = rightIdentifiers[index];
            var leftNumeric = int.TryParse(leftValue, out var leftNumber);
            var rightNumeric = int.TryParse(rightValue, out var rightNumber);

            if (leftNumeric && rightNumeric)
            {
                var numberComparison = leftNumber.CompareTo(rightNumber);
                if (numberComparison != 0)
                    return numberComparison;

                continue;
            }

            if (leftNumeric != rightNumeric)
                return leftNumeric ? -1 : 1;

            var textComparison = string.Compare(leftValue, rightValue, StringComparison.OrdinalIgnoreCase);
            if (textComparison != 0)
                return textComparison;
        }

        return 0;
    }

    internal readonly record struct ComparableVersion(int Major, int Minor, int Patch, string? PreRelease);

    internal sealed class ComparableVersionComparer : IComparer<string?>
    {
        public static ComparableVersionComparer Instance { get; } = new();

        public int Compare(string? x, string? y)
        {
            var leftParsed = TryParseComparableVersion(x, out var leftVersion);
            var rightParsed = TryParseComparableVersion(y, out var rightVersion);

            if (leftParsed && rightParsed)
                return GitHubReleaseUpdateService.Compare(leftVersion, rightVersion);

            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; set; } = [];
    }

    internal sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}