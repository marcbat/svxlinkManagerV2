using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Entities;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Events;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Domain.Aggregates.RadioProfil;

/// <summary>
/// Aggregate représentant un profil de configuration radio (Rx/Tx).
/// Utilisé par les Salons pour stocker les paramètres de réception et transmission.
/// Stream Marten : radioprofil-{guid}
/// </summary>
public class RadioProfilAggregate : AggregateRoot
{
    /// <summary>
    /// Nom du profil radio
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Configuration de réception
    /// </summary>
    public RxConfiguration RxConfiguration { get; private set; } = null!;

    /// <summary>
    /// Configuration de transmission
    /// </summary>
    public TxConfiguration TxConfiguration { get; private set; } = null!;

    /// <summary>
    /// Indique si le profil est supprimé (soft delete)
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Types de détection de squelch valides
    /// </summary>
    private static readonly string[] ValidSqlDetTypes = { "GPIO", "VOX", "CTCSS", "SERIAL", "EVDEV" };

    /// <summary>
    /// Constructeur par défaut requis pour Marten (rehydratation)
    /// </summary>
    public RadioProfilAggregate()
    {
    }

    /// <summary>
    /// Factory method pour créer un nouveau RadioProfil avec validations métier.
    /// Retourne un Validation&lt;Error, RadioProfilAggregate&gt; selon le Result Pattern.
    /// </summary>
    /// <param name="id">Identifiant unique du profil</param>
    /// <param name="name">Nom du profil</param>
    /// <param name="rxConfiguration">Configuration de réception</param>
    /// <param name="txConfiguration">Configuration de transmission</param>
    /// <returns>Validation contenant l'aggregate ou les erreurs de validation</returns>
    public static Validation<Error, RadioProfilAggregate> Create(
        Guid id,
        string name,
        RxConfiguration rxConfiguration,
        TxConfiguration txConfiguration)
    {
        // Validation de l'identifiant
        var idValidation = id.ValidateNotEmpty("Id");

        // Validation du nom
        var nameValidation = name.ValidateNotEmpty(
            "RADIOPROFIL_NAME_REQUIRED",
            "Le nom du profil radio est obligatoire");

        // Validations des configurations Rx/Tx
        var rxValidation = ValidateRxConfiguration(rxConfiguration);
        var txValidation = ValidateTxConfiguration(txConfiguration);

        // Combinaison de toutes les validations
        return (idValidation, nameValidation, rxValidation, txValidation)
            .Apply((validId, validName, validRx, validTx) =>
            {
                var aggregate = new RadioProfilAggregate();
                var @event = new RadioProfilCreatedEvent(
                    validId,
                    validName,
                    validRx,
                    validTx);

                aggregate.Apply(@event);
                aggregate.AddDomainEvent(@event);

                return aggregate;
            });
    }

    /// <summary>
    /// Mise à jour du profil radio
    /// </summary>
    /// <param name="name">Nouveau nom (optionnel)</param>
    /// <param name="rxConfiguration">Nouvelle configuration Rx (optionnel)</param>
    /// <param name="txConfiguration">Nouvelle configuration Tx (optionnel)</param>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> Update(
        string? name = null,
        RxConfiguration? rxConfiguration = null,
        TxConfiguration? txConfiguration = null)
    {
        if (IsDeleted)
            return Error.Validation("RADIOPROFIL_DELETED", "Le profil radio est supprimé")
                .ToFailure<Unit>();

        // Validation du nom si fourni
        var nameValidation = name != null
            ? name.ValidateNotEmpty("RADIOPROFIL_NAME_REQUIRED", "Le nom du profil radio est obligatoire")
            : Success<Error, string>(Name);

        // Validation RxConfiguration si fourni
        var rxValidation = rxConfiguration != null
            ? ValidateRxConfiguration(rxConfiguration)
            : Success<Error, RxConfiguration>(RxConfiguration);

        // Validation TxConfiguration si fourni
        var txValidation = txConfiguration != null
            ? ValidateTxConfiguration(txConfiguration)
            : Success<Error, TxConfiguration>(TxConfiguration);

        return (nameValidation, rxValidation, txValidation)
            .Apply((validName, validRx, validTx) =>
            {
                var @event = new RadioProfilUpdatedEvent(
                    Id,
                    name,
                    rxConfiguration,
                    txConfiguration);

                Apply(@event);
                AddDomainEvent(@event);

                return unit;
            });
    }

    /// <summary>
    /// Suppression logique du profil
    /// </summary>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> Delete()
    {
        if (IsDeleted)
            return Error.Validation("RADIOPROFIL_ALREADY_DELETED", "Le profil radio est déjà supprimé")
                .ToFailure<Unit>();

        var @event = new RadioProfilDeletedEvent(Id);
        Apply(@event);
        AddDomainEvent(@event);

        return unit.ToSuccess();
    }

    /// <summary>
    /// Applique l'événement RadioProfilCreatedEvent (Event Sourcing)
    /// </summary>
    public void Apply(RadioProfilCreatedEvent @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        RxConfiguration = @event.RxConfiguration;
        TxConfiguration = @event.TxConfiguration;
        IsDeleted = false;
    }

    /// <summary>
    /// Applique l'événement RadioProfilUpdatedEvent (Event Sourcing)
    /// </summary>
    public void Apply(RadioProfilUpdatedEvent @event)
    {
        if (@event.Name != null)
            Name = @event.Name;

        if (@event.RxConfiguration != null)
            RxConfiguration = @event.RxConfiguration;

        if (@event.TxConfiguration != null)
            TxConfiguration = @event.TxConfiguration;
    }

    /// <summary>
    /// Applique l'événement RadioProfilDeletedEvent (Event Sourcing)
    /// </summary>
    public void Apply(RadioProfilDeletedEvent @event)
    {
        IsDeleted = true;
    }

    /// <summary>
    /// Valide une configuration de réception
    /// </summary>
    private static Validation<Error, RxConfiguration> ValidateRxConfiguration(RxConfiguration rx)
    {
        var errors = new List<Error>();

        // Validation SqlDet
        if (!ValidSqlDetTypes.Contains(rx.SqlDet))
        {
            errors.Add(Error.Validation(
                "INVALID_SQL_DET",
                $"SqlDet doit être l'une des valeurs suivantes : {string.Join(", ", ValidSqlDetTypes)}"));
        }

        // Validation AudioDev format
        if (string.IsNullOrWhiteSpace(rx.AudioDev) || !rx.AudioDev.Contains(":"))
        {
            errors.Add(Error.Validation(
                "INVALID_AUDIO_DEV",
                "AudioDev doit avoir le format 'alsa:plughw:X'"));
        }

        // Validation AudioChannel
        if (rx.AudioChannel < 0)
        {
            errors.Add(Error.Validation(
                "INVALID_AUDIO_CHANNEL",
                "AudioChannel doit être >= 0"));
        }

        // Validation CTCSS
        if (rx.CtcssFq.HasValue && (rx.CtcssFq.Value < 0 || rx.CtcssFq.Value > 300))
        {
            errors.Add(Error.Validation(
                "INVALID_CTCSS_FQ",
                "CtcssFq doit être entre 0 et 300 Hz"));
        }

        // Validation delays et timings
        if (rx.SqlStartDelay <= 0)
            errors.Add(Error.Validation("INVALID_SQL_START_DELAY", "SqlStartDelay doit être > 0"));

        if (rx.SqlDelay <= 0)
            errors.Add(Error.Validation("INVALID_SQL_DELAY", "SqlDelay doit être > 0"));

        if (rx.SqlHangtime <= 0)
            errors.Add(Error.Validation("INVALID_SQL_HANGTIME", "SqlHangtime doit être > 0"));

        if (rx.SqlExtendedHangtime <= 0)
            errors.Add(Error.Validation("INVALID_SQL_EXTENDED_HANGTIME", "SqlExtendedHangtime doit être > 0"));

        return errors.Count > 0
            ? errors.ToFailure<RxConfiguration>()
            : rx.ToSuccess();
    }

    /// <summary>
    /// Valide une configuration de transmission
    /// </summary>
    private static Validation<Error, TxConfiguration> ValidateTxConfiguration(TxConfiguration tx)
    {
        var errors = new List<Error>();

        // Validation AudioDev format
        if (string.IsNullOrWhiteSpace(tx.AudioDev) || !tx.AudioDev.Contains(":"))
        {
            errors.Add(Error.Validation(
                "INVALID_AUDIO_DEV",
                "AudioDev doit avoir le format 'alsa:plughw:X'"));
        }

        // Validation AudioChannel
        if (tx.AudioChannel < 0)
        {
            errors.Add(Error.Validation(
                "INVALID_AUDIO_CHANNEL",
                "AudioChannel doit être >= 0"));
        }

        // Validation CTCSS
        if (tx.CtcssFq.HasValue && (tx.CtcssFq.Value < 0 || tx.CtcssFq.Value > 300))
        {
            errors.Add(Error.Validation(
                "INVALID_CTCSS_FQ",
                "CtcssFq doit être entre 0 et 300 Hz"));
        }

        // Validation TxDelay
        if (tx.TxDelay <= 0)
            errors.Add(Error.Validation("INVALID_TX_DELAY", "TxDelay doit être > 0"));

        return errors.Count > 0
            ? errors.ToFailure<TxConfiguration>()
            : tx.ToSuccess();
    }
}
