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
    public decimal DefaultRxFrequency { get; init; }
    public decimal DefaultTxFrequency { get; init; }

    public GeneralConfigurationCreated(
        Guid id,
        bool startReflectorOnStartup,
        bool startDefaultSalonOnStartup,
        decimal defaultRxFrequency,
        decimal defaultTxFrequency)
    {
        Id = id;
        StartReflectorOnStartup = startReflectorOnStartup;
        StartDefaultSalonOnStartup = startDefaultSalonOnStartup;
        DefaultRxFrequency = defaultRxFrequency;
        DefaultTxFrequency = defaultTxFrequency;
    }
}
