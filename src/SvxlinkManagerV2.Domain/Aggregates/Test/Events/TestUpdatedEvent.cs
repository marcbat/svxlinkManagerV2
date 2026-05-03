namespace SvxlinkManagerV2.Domain.Aggregates.Test.Events;

/// <summary>
/// Événement émis lors de la mise à jour d'un TestAggregate
/// </summary>
public record TestUpdatedEvent : Common.DomainEvent
{
    public Guid Id { get; init; }
    public string NewValue { get; init; } = string.Empty;

    public TestUpdatedEvent(Guid id, string newValue)
    {
        Id = id;
        NewValue = newValue;
    }
}
