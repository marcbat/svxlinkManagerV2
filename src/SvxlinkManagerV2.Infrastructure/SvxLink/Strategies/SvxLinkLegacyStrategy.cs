using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.Strategies;

/// <summary>
/// Strategy for SVXLink 19.09.2 (legacy) installed in /opt/svxlink-legacy.
/// Used for ReflectorV2 protocol (AUTH_KEY, protocol v1.0).
/// </summary>
public class SvxLinkLegacyStrategy : ISvxLinkVersionStrategy
{
    private const string Prefix = "/opt/svxlink-legacy";

    public ReflectorProtocol Protocol => ReflectorProtocol.V2;

    public string DisplayName => "SVXLink legacy";

    public string Version => "19.09.2";

    public bool IsInstalled => File.Exists(BinaryPath);

    public string BinaryPath => $"{Prefix}/bin/svxlink";

    public string LibraryPath => $"{Prefix}/lib";

    public string ConfigDirectory => $"{Prefix}/etc/svxlink";

    public string SoundsDirectory => $"{Prefix}/share/svxlink/sounds/fr_FR/svxlinkmanager";

    public string EventsDirectory => $"{Prefix}/share/svxlink/events.d/local";

    public IReadOnlyDictionary<string, string> EnvironmentVariables => new Dictionary<string, string>
    {
        ["LD_LIBRARY_PATH"] = LibraryPath
    };
}
