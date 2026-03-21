using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Domain.Aggregates.Reflector;

/// <summary>
/// Aggregate représentant un SvxReflector (serveur de conférence pour nœuds SvxLink).
/// La configuration est stockée sous forme de texte INI brut pour une flexibilité maximale.
/// Stream Marten : reflector-{guid}
/// </summary>
public class ReflectorAggregate : AggregateRoot
{
    /// <summary>
    /// Nom descriptif du reflector (ex: "SvxReflector Local")
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Contenu brut du fichier de configuration INI svxreflector.conf.
    /// Édité librement par l'utilisateur, écrit tel quel dans /etc/svxlink/svxreflector.conf.
    /// </summary>
    public string Config { get; private set; } = string.Empty;

    /// <summary>
    /// Indique si le daemon svxreflector est actuellement actif
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Indique si le reflector est supprimé (soft delete)
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Constructeur par défaut requis pour Marten (rehydratation)
    /// </summary>
    public ReflectorAggregate()
    {
    }

    /// <summary>
    /// Factory method pour créer un nouveau Reflector avec validations métier.
    /// Retourne un Validation&lt;Error, ReflectorAggregate&gt; selon le Result Pattern.
    /// </summary>
    /// <param name="id">Identifiant unique du reflector</param>
    /// <param name="name">Nom descriptif</param>
    /// <param name="config">Contenu INI brut de svxreflector.conf</param>
    /// <returns>Validation contenant l'aggregate ou les erreurs de validation</returns>
    public static Validation<Error, ReflectorAggregate> Create(
        Guid id,
        string name,
        string config)
    {
        var idValidation = id.ValidateNotEmpty("Id");

        var nameValidation = name.ValidateNotEmpty(
            "REFLECTOR_NAME_REQUIRED",
            "Le nom du reflector est obligatoire");

        var configValidation = ValidateConfig(config);

        return (idValidation, nameValidation, configValidation)
            .Apply((validId, validName, validConfig) =>
            {
                var aggregate = new ReflectorAggregate();
                var @event = new ReflectorCreated(validId, validName, validConfig);

                aggregate.Apply(@event);
                aggregate.AddDomainEvent(@event);

                return aggregate;
            });
    }

    /// <summary>
    /// Met à jour le nom et la configuration du reflector.
    /// Bloqué si le reflector est actif ou supprimé.
    /// </summary>
    /// <param name="name">Nouveau nom</param>
    /// <param name="config">Nouveau contenu INI brut</param>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> UpdateConfiguration(string name, string config)
    {
        if (IsDeleted)
            return Error.Validation("REFLECTOR_DELETED", "Le reflector est supprimé")
                .ToFailure<Unit>();

        if (IsActive)
            return Error.Validation("REFLECTOR_ACTIVE", "Impossible de modifier un reflector actif. Arrêtez-le d'abord.")
                .ToFailure<Unit>();

        var nameValidation = name.ValidateNotEmpty(
            "REFLECTOR_NAME_REQUIRED",
            "Le nom du reflector est obligatoire");

        var configValidation = ValidateConfig(config);

        return (nameValidation, configValidation)
            .Apply((validName, validConfig) =>
            {
                var @event = new ReflectorConfigurationUpdated(Id, validName, validConfig);
                Apply(@event);
                AddDomainEvent(@event);
                return unit;
            });
    }

    /// <summary>
    /// Active le reflector (démarre le daemon svxreflector).
    /// </summary>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> Activate()
    {
        if (IsDeleted)
            return Error.Validation("REFLECTOR_DELETED", "Le reflector est supprimé")
                .ToFailure<Unit>();

        if (IsActive)
            return Error.Validation("REFLECTOR_ALREADY_ACTIVE", "Le reflector est déjà actif")
                .ToFailure<Unit>();

        var @event = new ReflectorActivated(Id);
        Apply(@event);
        AddDomainEvent(@event);

        return unit.ToSuccess();
    }

    /// <summary>
    /// Désactive le reflector (arrête le daemon svxreflector).
    /// </summary>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> Deactivate()
    {
        if (IsDeleted)
            return Error.Validation("REFLECTOR_DELETED", "Le reflector est supprimé")
                .ToFailure<Unit>();

        if (!IsActive)
            return Error.Validation("REFLECTOR_ALREADY_INACTIVE", "Le reflector est déjà arrêté")
                .ToFailure<Unit>();

        var @event = new ReflectorDeactivated(Id);
        Apply(@event);
        AddDomainEvent(@event);

        return unit.ToSuccess();
    }

    /// <summary>
    /// Suppression logique du reflector.
    /// Bloqué si le reflector est actif.
    /// </summary>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> Delete()
    {
        if (IsDeleted)
            return Error.Validation("REFLECTOR_ALREADY_DELETED", "Le reflector est déjà supprimé")
                .ToFailure<Unit>();

        if (IsActive)
            return Error.Validation("REFLECTOR_ACTIVE", "Impossible de supprimer un reflector actif. Arrêtez-le d'abord.")
                .ToFailure<Unit>();

        var @event = new ReflectorDeleted(Id);
        Apply(@event);
        AddDomainEvent(@event);

        return unit.ToSuccess();
    }

    #region Event Sourcing - Apply Methods

    /// <summary>
    /// Applique l'événement ReflectorCreated (Event Sourcing)
    /// </summary>
    public void Apply(ReflectorCreated @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Config = @event.Config;
        IsActive = false;
        IsDeleted = false;
    }

    /// <summary>
    /// Applique l'événement ReflectorConfigurationUpdated (Event Sourcing)
    /// </summary>
    public void Apply(ReflectorConfigurationUpdated @event)
    {
        Name = @event.Name;
        Config = @event.Config;
    }

    /// <summary>
    /// Applique l'événement ReflectorActivated (Event Sourcing)
    /// </summary>
    public void Apply(ReflectorActivated @event)
    {
        IsActive = true;
    }

    /// <summary>
    /// Applique l'événement ReflectorDeactivated (Event Sourcing)
    /// </summary>
    public void Apply(ReflectorDeactivated @event)
    {
        IsActive = false;
    }

    /// <summary>
    /// Applique l'événement ReflectorDeleted (Event Sourcing)
    /// </summary>
    public void Apply(ReflectorDeleted @event)
    {
        IsDeleted = true;
    }

    #endregion

    #region Validation

    private static Validation<Error, string> ValidateConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return Error.Validation("REFLECTOR_CONFIG_REQUIRED", "La configuration du reflector est obligatoire")
                .ToFailure<string>();

        if (!config.Contains("[GLOBAL]", StringComparison.OrdinalIgnoreCase))
            return Error.Validation("REFLECTOR_CONFIG_INVALID", "La configuration doit contenir une section [GLOBAL]")
                .ToFailure<string>();

        return config.ToSuccess();
    }

    #endregion
}
