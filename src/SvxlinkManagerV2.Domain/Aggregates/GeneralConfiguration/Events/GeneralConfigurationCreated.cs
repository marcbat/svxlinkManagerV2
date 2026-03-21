using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Events;

/// <summary>
/// Événement émis lors de la création de la configuration générale.
/// </summary>
public record GeneralConfigurationCreated : DomainEvent
{
    public Guid Id { get; init; }
    public bool StartReflectorOnStartup { get; init; }
    public bool StartDefaultSalonOnStartup { get; init; }

    public GeneralConfigurationCreated(Guid id, bool startReflectorOnStartup, bool startDefaultSalonOnStartup)
    {
        Id = id;
        StartReflectorOnStartup = startReflectorOnStartup;
        StartDefaultSalonOnStartup = startDefaultSalonOnStartup;
    }
}
