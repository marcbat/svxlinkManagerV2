using SvxlinkManagerV2.Application.Features.ApplicationUpdate;

namespace SvxlinkManagerV2.Infrastructure.Network;

/// <summary>
/// Options de consultation des releases GitHub pour les mises à jour applicatives.
/// </summary>
public class ApplicationUpdateOptions
{
    public const string SectionName = "ApplicationUpdate";

    public bool Enabled { get; set; } = true;

    public string Owner { get; set; } = "marcbat";

    public string Repository { get; set; } = "svxlinkManagerV2";

    public string? GitHubToken { get; set; }

    public string PackagePattern { get; set; } = "*.deb";

    public ApplicationUpdateChannel Channel { get; set; } = ApplicationUpdateChannel.Stable;

    public string StagingDirectory { get; set; } = "data/updates";

    public string? InstallCommand { get; set; }

    public string? InstallArguments { get; set; } = "{packagePath}";

    public int InstallCommandTimeoutSeconds { get; set; } = 300;
}