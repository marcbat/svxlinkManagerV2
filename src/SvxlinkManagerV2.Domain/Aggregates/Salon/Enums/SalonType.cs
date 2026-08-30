namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

/// <summary>
/// Type of salon determining its operating mode.
/// </summary>
public enum SalonType
{
    /// <summary>
    /// Standard salon connected to a remote SVXReflector.
    /// </summary>
    Reflector = 0,

    /// <summary>
    /// Simplex parrot/echo salon that records and plays back audio locally.
    /// </summary>
    Parrot = 1
}
