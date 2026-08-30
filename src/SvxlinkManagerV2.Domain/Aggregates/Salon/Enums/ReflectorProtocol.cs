namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

/// <summary>
/// Protocol version for connecting to a SvxReflector server.
/// </summary>
public enum ReflectorProtocol
{
    /// <summary>
    /// Modern protocol (SVXLink 25.05+) using X.509 certificates and encryption.
    /// </summary>
    V3 = 0,

    /// <summary>
    /// Legacy protocol (SVXLink 19.09.2) using AUTH_KEY shared secret.
    /// </summary>
    V2 = 1
}
