using SvxlinkManagerV2.Domain.Aggregates.Test.Events;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Test;

/// <summary>
/// Aggregate de test pour valider le fonctionnement d'Event Sourcing avec Marten.
/// Cet aggregate démontre comment reconstruire l'état depuis un stream d'événements.
/// </summary>
public class TestAggregate : AggregateRoot
{
    /// <summary>
    /// Valeur stockée dans l'aggregate
    /// </summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>
    /// Constructeur par défaut requis pour Marten (rehydratation)
    /// </summary>
    public TestAggregate()
    {
    }

    /// <summary>
    /// Factory method pour créer un nouveau TestAggregate
    /// </summary>
    public static TestAggregate Create(Guid id, string value)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant ne peut pas être vide", nameof(id));
        
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("La valeur ne peut pas être vide", nameof(value));

        var aggregate = new TestAggregate();
        var @event = new TestCreatedEvent(id, value);
        
        // Applique l'événement localement
        aggregate.Apply(@event);
        
        // Ajoute à la collection d'événements non commités
        aggregate.AddDomainEvent(@event);
        
        return aggregate;
    }

    /// <summary>
    /// Mise à jour de la valeur
    /// </summary>
    public void UpdateValue(string newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
            throw new ArgumentException("La nouvelle valeur ne peut pas être vide", nameof(newValue));

        if (Value == newValue)
            return; // Pas de changement

        var @event = new TestUpdatedEvent(Id, newValue);
        
        // Applique l'événement localement
        Apply(@event);
        
        // Ajoute à la collection d'événements non commités
        AddDomainEvent(@event);
    }

    /// <summary>
    /// Applique un événement TestCreatedEvent (Event Sourcing).
    /// Utilisé lors de la rehydratation depuis le stream d'événements.
    /// </summary>
    public void Apply(TestCreatedEvent @event)
    {
        Id = @event.Id;
        Value = @event.Value;
    }

    /// <summary>
    /// Applique un événement TestUpdatedEvent (Event Sourcing).
    /// Utilisé lors de la rehydratation depuis le stream d'événements.
    /// </summary>
    public void Apply(TestUpdatedEvent @event)
    {
        Value = @event.NewValue;
    }
}
