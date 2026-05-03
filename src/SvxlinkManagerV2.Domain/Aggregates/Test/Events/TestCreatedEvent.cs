namespace SvxlinkManagerV2.Domain.Aggregates.Test.Events;

/// <summary>
/// Événement émis lors de la création d'un TestAggregate
/// </summary>
public record TestCreatedEvent : Common.DomainEvent
{
    public Guid Id { get; init; }
    public string Value { get; init; } = string.Empty;

    public TestCreatedEvent(Guid id, string value)
    {
        Id = id;
        Value = value;
    }
}
