using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Events;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;

/// <summary>
/// Aggregate représentant la configuration générale de l'application.
/// Il n'existe qu'une seule instance de cet aggregate (ID fixe).
/// Gère les options de démarrage automatique (réflecteur, salon par défaut).
/// Stream Marten : generalconfiguration-00000000-0000-0000-0000-000000000003
/// </summary>
public class GeneralConfigurationAggregate : AggregateRoot
{
    /// <summary>
    /// ID fixe de la configuration générale (une seule instance par application)
    /// </summary>
    public static readonly Guid FixedId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    /// <summary>
    /// Indique si le réflecteur doit démarrer automatiquement au lancement de l'application.
    /// </summary>
    public bool StartReflectorOnStartup { get; private set; }

    /// <summary>
    /// Indique si le salon par défaut doit démarrer automatiquement au lancement de l'application.
    /// </summary>
    public bool StartDefaultSalonOnStartup { get; private set; }

    /// <summary>
    /// Constructeur par défaut requis pour Marten (rehydratation)
    /// </summary>
    public GeneralConfigurationAggregate() { }

    /// <summary>
    /// Factory method pour créer la configuration générale avec l'ID fixe.
    /// </summary>
    public static Validation<Error, GeneralConfigurationAggregate> Create(
        bool startReflectorOnStartup = false,
        bool startDefaultSalonOnStartup = false)
    {
        var aggregate = new GeneralConfigurationAggregate();
        var @event = new GeneralConfigurationCreated(FixedId, startReflectorOnStartup, startDefaultSalonOnStartup);

        aggregate.Apply(@event);
        aggregate.AddDomainEvent(@event);

        return aggregate;
    }

    /// <summary>
    /// Met à jour la configuration générale.
    /// </summary>
    public Validation<Error, Unit> Update(
        bool startReflectorOnStartup,
        bool startDefaultSalonOnStartup)
    {
        var @event = new GeneralConfigurationUpdated(startReflectorOnStartup, startDefaultSalonOnStartup);

        Apply(@event);
        AddDomainEvent(@event);

        return unit.ToSuccess();
    }

    #region Apply

    /// <summary>
    /// Applique l'événement GeneralConfigurationCreated (Event Sourcing)
    /// </summary>
    public void Apply(GeneralConfigurationCreated @event)
    {
        Id = @event.Id;
        StartReflectorOnStartup = @event.StartReflectorOnStartup;
        StartDefaultSalonOnStartup = @event.StartDefaultSalonOnStartup;
    }

    /// <summary>
    /// Applique l'événement GeneralConfigurationUpdated (Event Sourcing)
    /// </summary>
    public void Apply(GeneralConfigurationUpdated @event)
    {
        StartReflectorOnStartup = @event.StartReflectorOnStartup;
        StartDefaultSalonOnStartup = @event.StartDefaultSalonOnStartup;
    }

    #endregion
}
