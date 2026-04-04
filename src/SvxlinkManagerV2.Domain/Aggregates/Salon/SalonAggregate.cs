using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Events;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using System.Text.RegularExpressions;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon;

/// <summary>
/// Aggregate représentant un Salon (connexion à un SVXLink Reflector).
/// Un Salon contient toute la configuration pour se connecter à un reflector avec ses paramètres audio,
/// d'authentification et de logique SVXLink.
/// Stream Marten : salon-{guid}
/// </summary>
public class SalonAggregate : AggregateRoot
{
    /// <summary>
    /// Nom du salon (ex: "Salon National France", "Salon Bayern")
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Indique si c'est le salon par défaut (activé automatiquement au démarrage)
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>
    /// Indique si le salon est temporisé (activation automatique selon planning horaire)
    /// </summary>
    public bool IsTemporized { get; private set; }

    /// <summary>
    /// Configuration complète SVXLink pour ce salon
    /// </summary>
    public SvxLinkConfiguration Configuration { get; private set; } = null!;

    /// <summary>
    /// Indique si le salon est supprimé (soft delete)
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Code DTMF optionnel pour changer de salon par commande radio (1-9999)
    /// </summary>
    public int? DtmfCode { get; private set; }

    /// <summary>
    /// Pattern regex pour validation du format d'indicatif radioamateur
    /// Format: one à deux lettres + chiffre + lettres/chiffres + optionnel tiret et suffixe
    /// Exemples valides: F5ABC, F5ABC-L, W1AW, KB2XYZ-R
    /// </summary>
    private static readonly Regex CallsignPattern = new(@"^[A-Z]{1,2}\d[A-Z0-9]{1,4}(-[A-Z0-9]{1,2})?$", RegexOptions.Compiled);

    /// <summary>
    /// Codecs audio valides supportés par SVXLink
    /// </summary>
    private static readonly string[] ValidAudioCodecs = { "OPUS", "GSM", "SPEEX", "S16" };

    /// <summary>
    /// Constructeur par défaut requis pour Marten (rehydratation)
    /// </summary>
    public SalonAggregate()
    {
    }

    /// <summary>
    /// Factory method pour créer un nouveau Salon avec validations métier complètes.
    /// Retourne un Validation&lt;Error, SalonAggregate&gt; selon le Result Pattern.
    /// </summary>
    /// <param name="id">Identifiant unique du salon</param>
    /// <param name="name">Nom du salon</param>
    /// <param name="isDefault">Si c'est le salon par défaut</param>
    /// <param name="isTemporized">Si le salon est temporisé</param>
    /// <param name="configuration">Configuration SVXLink complète</param>
    /// <returns>Validation contenant l'aggregate ou les erreurs de validation</returns>
    public static Validation<Error, SalonAggregate> Create(
        Guid id,
        string name,
        bool isDefault,
        bool isTemporized,
        SvxLinkConfiguration configuration)
    {
        // Validation de l'identifiant
        var idValidation = id.ValidateNotEmpty("Id");

        // Validation du nom
        var nameValidation = name.ValidateNotEmpty(
            "SALON_NAME_REQUIRED",
            "Le nom du salon est obligatoire");

        // Validation de la configuration
        var configValidation = ValidateConfiguration(configuration);

        // Combinaison de toutes les validations
        return (idValidation, nameValidation, configValidation)
            .Apply((validId, validName, validConfig) =>
            {
                var aggregate = new SalonAggregate();
                var @event = new SalonCreated(
                    validId,
                    validName,
                    isDefault,
                    isTemporized,
                    validConfig);

                aggregate.Apply(@event);
                aggregate.AddDomainEvent(@event);

                return aggregate;
            });
    }

    /// <summary>
    /// Met à jour la configuration du salon
    /// </summary>
    /// <param name="configuration">Nouvelle configuration</param>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> UpdateConfiguration(SvxLinkConfiguration configuration)
    {
        if (IsDeleted)
            return Error.Validation("SALON_DELETED", "Le salon est supprimé")
                .ToFailure<Unit>();

        // Validation de la configuration
        var configValidation = ValidateConfiguration(configuration);

        return configValidation.Map(validConfig =>
        {
            var @event = new SalonConfigurationUpdated(Id, validConfig);
            Apply(@event);
            AddDomainEvent(@event);
            return unit;
        });
    }

    /// <summary>
    /// Définit ce salon comme salon par défaut (activé automatiquement au démarrage)
    /// </summary>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> SetAsDefault()
    {
        if (IsDeleted)
            return Error.Validation("SALON_DELETED", "Le salon est supprimé")
                .ToFailure<Unit>();

        if (IsDefault)
            return Error.Validation("SALON_ALREADY_DEFAULT", "Le salon est déjà le salon par défaut")
                .ToFailure<Unit>();

        var @event = new SalonSetAsDefault(Id);
        Apply(@event);
        AddDomainEvent(@event);

        return unit.ToSuccess();
    }

    /// <summary>
    /// Retire à ce salon son statut de salon par défaut
    /// </summary>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> UnsetDefault()
    {
        if (IsDeleted)
            return Error.Validation("SALON_DELETED", "Le salon est supprimé")
                .ToFailure<Unit>();

        if (!IsDefault)
            return Error.Validation("SALON_NOT_DEFAULT", "Le salon n'est pas le salon par défaut")
                .ToFailure<Unit>();

        var @event = new SalonUnsetDefault(Id);
        Apply(@event);
        AddDomainEvent(@event);

        return unit.ToSuccess();
    }

    /// <summary>
    /// Suppression logique du salon
    /// </summary>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> Delete()
    {
        if (IsDeleted)
            return Error.Validation("SALON_ALREADY_DELETED", "Le salon est déjà supprimé")
                .ToFailure<Unit>();

        if (IsDefault)
            return Error.Validation("SALON_IS_DEFAULT", "Impossible de supprimer le salon par défaut")
                .ToFailure<Unit>();

        var @event = new SalonDeleted(Id);
        Apply(@event);
        AddDomainEvent(@event);

        return unit.ToSuccess();
    }

    /// <summary>
    /// Met à jour le code DTMF du salon
    /// </summary>
    /// <param name="dtmfCode">Code DTMF (null pour supprimer, 1-9999 pour définir)</param>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> UpdateDtmfCode(int? dtmfCode)
    {
        if (IsDeleted)
            return Error.Validation("SALON_DELETED", "Le salon est supprimé")
                .ToFailure<Unit>();

        if (dtmfCode.HasValue && (dtmfCode.Value < 1 || dtmfCode.Value > 9999))
            return Error.Validation("DTMF_CODE_INVALID", "Le code DTMF doit être entre 1 et 9999")
                .ToFailure<Unit>();

        var @event = new SalonDtmfCodeUpdated(Id, dtmfCode);
        Apply(@event);
        AddDomainEvent(@event);

        return unit.ToSuccess();
    }

    #region Event Sourcing - Apply Methods

    /// <summary>
    /// Applique l'événement SalonCreated (Event Sourcing)
    /// </summary>
    public void Apply(SalonCreated @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        IsDefault = @event.IsDefault;
        IsTemporized = @event.IsTemporized;
        Configuration = @event.Configuration;
        IsDeleted = false;
    }

    /// <summary>
    /// Applique l'événement SalonConfigurationUpdated (Event Sourcing)
    /// </summary>
    public void Apply(SalonConfigurationUpdated @event)
    {
        Configuration = @event.Configuration;
    }
    public void Apply(SalonDeleted @event)
    {
        IsDeleted = true;
    }

    /// <summary>
    /// Applique l'événement SalonSetAsDefault (Event Sourcing)
    /// </summary>
    public void Apply(SalonSetAsDefault @event)
    {
        IsDefault = true;
    }

    /// <summary>
    /// Applique l'événement SalonUnsetDefault (Event Sourcing)
    /// </summary>
    public void Apply(SalonUnsetDefault @event)
    {
        IsDefault = false;
    }

    /// <summary>
    /// Applique l'événement SalonDtmfCodeUpdated (Event Sourcing)
    /// </summary>
    public void Apply(SalonDtmfCodeUpdated @event)
    {
        DtmfCode = @event.DtmfCode;
    }

    #endregion

    #region Validations

    /// <summary>
    /// Valide une configuration SVXLink complète
    /// </summary>
    private static Validation<Error, SvxLinkConfiguration> ValidateConfiguration(SvxLinkConfiguration config)
    {
        var errors = new List<Error>();

        // Validation Host (obligatoire)
        if (string.IsNullOrWhiteSpace(config.Host))
        {
            errors.Add(Error.Validation(
                "SALON_HOST_REQUIRED",
                "L'hôte du reflector est obligatoire"));
        }
        else
        {
            // Validation du format Host (domaine ou IP)
            var hostPattern = new Regex(@"^([a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z]{2,}$|^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$");
            if (!hostPattern.IsMatch(config.Host))
            {
                errors.Add(Error.Validation(
                    "SALON_HOST_INVALID",
                    "Le format de l'hôte est invalide (domaine ou IP attendu)"));
            }
        }

        // Validation Port (1-65535)
        if (config.Port < 1 || config.Port > 65535)
        {
            errors.Add(Error.Validation(
                "SALON_PORT_INVALID",
                "Le port doit être entre 1 et 65535"));
        }

        // Validation Callsign (obligatoire, format libre pour SVXReflector)
        if (string.IsNullOrWhiteSpace(config.Callsign))
        {
            errors.Add(Error.Validation(
                "SALON_CALLSIGN_REQUIRED",
                "L'indicatif est obligatoire"));
        }

        // Validation AuthKey (obligatoire)
        if (string.IsNullOrWhiteSpace(config.AuthKey))
        {
            errors.Add(Error.Validation(
                "SALON_AUTHKEY_REQUIRED",
                "La clé d'authentification est obligatoire"));
        }

        // Validation AudioCodec (doit être dans la liste valide)
        if (!ValidAudioCodecs.Contains(config.AudioCodec.ToUpperInvariant()))
        {
            errors.Add(Error.Validation(
                "SALON_AUDIOCODEC_INVALID",
                $"Le codec audio doit être parmi: {string.Join(", ", ValidAudioCodecs)}"));
        }

        // Validation RxFrequency (obligatoire, plage 30-3000 MHz)
        if (config.RxFrequency < 30 || config.RxFrequency > 3000)
        {
            errors.Add(Error.Validation(
                "SALON_RXFREQUENCY_INVALID",
                "La fréquence de réception doit être entre 30 et 3000 MHz"));
        }

        // Validation TxFrequency (obligatoire, plage 30-3000 MHz)
        if (config.TxFrequency < 30 || config.TxFrequency > 3000)
        {
            errors.Add(Error.Validation(
                "SALON_TXFREQUENCY_INVALID",
                "La fréquence de transmission doit être entre 30 et 3000 MHz"));
        }

        // Validation RxCtcss (si défini, plage 67.0-250.3 Hz)
        if (config.RxCtcss.HasValue && (config.RxCtcss.Value < 67.0m || config.RxCtcss.Value > 250.3m))
        {
            errors.Add(Error.Validation(
                "SALON_RXCTCSS_INVALID",
                "La tonalité CTCSS de réception doit être entre 67.0 et 250.3 Hz"));
        }

        // Validation TxCtcss (si défini, plage 67.0-250.3 Hz)
        if (config.TxCtcss.HasValue && (config.TxCtcss.Value < 67.0m || config.TxCtcss.Value > 250.3m))
        {
            errors.Add(Error.Validation(
                "SALON_TXCTCSS_INVALID",
                "La tonalité CTCSS de transmission doit être entre 67.0 et 250.3 Hz"));
        }

        // Validation SimplexCallsign (obligatoire et format radioamateur)
        if (string.IsNullOrWhiteSpace(config.SimplexCallsign))
        {
            errors.Add(Error.Validation(
                "SALON_SIMPLEX_CALLSIGN_REQUIRED",
                "L'indicatif simplex est obligatoire"));
        }
        else if (!CallsignPattern.IsMatch(config.SimplexCallsign))
        {
            errors.Add(Error.Validation(
                "SALON_SIMPLEX_CALLSIGN_INVALID",
                "Le format de l'indicatif simplex est invalide (format radioamateur attendu)"));
        }

        // Validation CardSampleRate (valeurs standards: 8000, 16000, 48000)
        var validSampleRates = new[] { 8000, 16000, 48000 };
        if (!validSampleRates.Contains(config.CardSampleRate))
        {
            errors.Add(Error.Validation(
                "SALON_SAMPLERATE_INVALID",
                "Le taux d'échantillonnage doit être 8000, 16000 ou 48000 Hz"));
        }

        // Validation CardChannels (1 ou 2)
        if (config.CardChannels < 1 || config.CardChannels > 2)
        {
            errors.Add(Error.Validation(
                "SALON_CHANNELS_INVALID",
                "Le nombre de canaux doit être 1 (mono) ou 2 (stereo)"));
        }

        // Validation intervalles d'identification (min 5 secondes, max 3600 secondes = 1h)
        if (config.ShortIdentInterval < 5 || config.ShortIdentInterval > 3600)
        {
            errors.Add(Error.Validation(
                "SALON_SHORT_IDENT_INTERVAL_INVALID",
                "L'intervalle d'identification court doit être entre 5 et 3600 secondes"));
        }

        if (config.LongIdentInterval < 5 || config.LongIdentInterval > 3600)
        {
            errors.Add(Error.Validation(
                "SALON_LONG_IDENT_INTERVAL_INVALID",
                "L'intervalle d'identification long doit être entre 5 et 3600 secondes"));
        }

        // Si des erreurs existent, retourner Failure
        if (errors.Any())
        {
            return errors.ToFailure<SvxLinkConfiguration>();
        }

        return config.ToSuccess();
    }

    #endregion
}
