using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Events;

/// <summary>
/// Événement émis lors de la mise à jour de la configuration générale.
/// </summary>
public record GeneralConfigurationUpdated : DomainEvent
{
    public bool StartReflectorOnStartup { get; init; }
    public bool StartDefaultSalonOnStartup { get; init; }

    public GeneralConfigurationUpdated(bool startReflectorOnStartup, bool startDefaultSalonOnStartup)
    {
        StartReflectorOnStartup = startReflectorOnStartup;
        StartDefaultSalonOnStartup = startDefaultSalonOnStartup;
    }
}
