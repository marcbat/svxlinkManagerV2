namespace SvxlinkManagerV2.Application.Features.ApplicationUpdate;

/// <summary>
/// Canal de mise à jour consulté pour l'application.
/// </summary>
public enum ApplicationUpdateChannel
{
    Stable,
    Prerelease,
    Feature
}

/// <summary>
/// Informations d'une release distante disponible au téléchargement.
/// </summary>
public record ApplicationReleaseInfo(
    string Version,
    string Tag,
    string Name,
    DateTimeOffset PublishedAt,
    bool IsPrerelease,
    string? ReleaseNotesUrl,
    string? ChecksumUrl,
    string? PackageUrl,
    string? PackageName,
    long? PackageAssetId = null,
    long? ChecksumAssetId = null);

/// <summary>
/// Statut courant de la mise à jour applicative.
/// </summary>
public record ApplicationUpdateStatusDto(
    string CurrentVersion,
    ApplicationUpdateChannel Channel,
    bool IsConfigured,
    bool IsUpdateAvailable,
    ApplicationReleaseInfo? LatestRelease,
    string? Message);