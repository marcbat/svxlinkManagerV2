using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Defines the version-specific paths and settings for a SVXLink installation.
/// Each implementation targets a specific SVXLink version installed in an isolated prefix.
/// </summary>
public interface ISvxLinkVersionStrategy
{
    /// <summary>
    /// The reflector protocol supported by this SVXLink version.
    /// </summary>
    ReflectorProtocol Protocol { get; }

    /// <summary>
    /// Human readable name of this installation (e.g. "SVXLink legacy").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Upstream SVXLink version bundled in this installation (e.g. 19.09.2).
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Indicates whether this SVXLink installation is actually present on the machine.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Absolute path to the svxlink binary (e.g. /opt/svxlink-legacy/bin/svxlink).
    /// </summary>
    string BinaryPath { get; }

    /// <summary>
    /// Absolute path to the version's shared libraries (e.g. /opt/svxlink-legacy/lib).
    /// </summary>
    string LibraryPath { get; }

    /// <summary>
    /// Absolute path to the configuration directory (e.g. /opt/svxlink-legacy/etc/svxlink).
    /// </summary>
    string ConfigDirectory { get; }

    /// <summary>
    /// Absolute path to the sounds directory (e.g. /opt/svxlink-legacy/share/svxlink/sounds/fr_FR/svxlinkmanager).
    /// </summary>
    string SoundsDirectory { get; }

    /// <summary>
    /// Absolute path to the events directory (e.g. /opt/svxlink-legacy/share/svxlink/events.d/local).
    /// </summary>
    string EventsDirectory { get; }

    /// <summary>
    /// Environment variables required to run this SVXLink version (e.g. LD_LIBRARY_PATH).
    /// </summary>
    IReadOnlyDictionary<string, string> EnvironmentVariables { get; }
}
