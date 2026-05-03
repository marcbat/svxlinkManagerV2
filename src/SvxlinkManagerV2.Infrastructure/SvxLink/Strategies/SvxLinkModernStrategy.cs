using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.Strategies;

/// <summary>
/// Strategy for SVXLink 25.05 (modern) installed in /opt/svxlink-modern.
/// Used for ReflectorV3 protocol (X.509 certificates, protocol v3.0).
/// </summary>
public class SvxLinkModernStrategy : ISvxLinkVersionStrategy
{
    private const string Prefix = "/opt/svxlink-modern";

    public ReflectorProtocol Protocol => ReflectorProtocol.V3;

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
