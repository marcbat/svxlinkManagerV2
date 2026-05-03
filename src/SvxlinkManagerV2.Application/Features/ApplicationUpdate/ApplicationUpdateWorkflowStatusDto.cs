namespace SvxlinkManagerV2.Application.Features.ApplicationUpdate;

/// <summary>
/// Etat opérationnel courant du workflow de mise à jour applicative.
/// </summary>
public enum ApplicationUpdateOperationState
{
    Idle,
    Downloading,
    Downloaded,
    InstallRequested,
    Failed
}

/// <summary>
/// Métadonnées du paquet téléchargé localement.
/// </summary>
public record ApplicationDownloadedPackageInfo(
    string Version,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    DateTimeOffset DownloadedAt,
    string? SourceUrl);

/// <summary>
/// Statut complet du workflow de mise à jour côté application.
/// </summary>
public record ApplicationUpdateWorkflowStatusDto(
    ApplicationUpdateStatusDto UpdateStatus,
    ApplicationUpdateOperationState OperationState,
    int? DownloadProgressPercent,
    ApplicationDownloadedPackageInfo? DownloadedPackage,
    bool IsBusy,
    bool CanDownload,
    bool CanRequestInstall,
    string? LastOperationMessage,
    DateTimeOffset UpdatedAt);