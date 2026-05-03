using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.SA818.Events;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Domain.Aggregates.SA818;

/// <summary>
/// Aggregate représentant le module radio SA818 physique unique.
/// Cet aggregate possède un ID fixe car il n'existe qu'un seul module SA818 physique.
/// Il gère les paramètres hardware globaux (volume, squelch, filtres).
/// Les fréquences et CTCSS sont gérés par l'aggregate Salon.
/// Stream Marten : sa818-00000000-0000-0000-0000-000000000001
/// </summary>
public class SA818Aggregate : AggregateRoot
{
    /// <summary>
    /// ID fixe du SA818 (un seul device physique)
    /// </summary>
    public static readonly Guid FixedId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Volume audio (plage valide: 1-8)
    /// 1 = volume minimum, 8 = volume maximum
    /// </summary>
    public int Volume { get; private set; }

    /// <summary>
    /// Niveau de squelch (plage valide: 0-8)
    /// 0 = squelch désactivé, 8 = squelch maximum
    /// </summary>
    public int Squelch { get; private set; }

    /// <summary>
    /// Largeur de bande (12.5kHz ou 25kHz)
    /// </summary>
    public SA818Bandwidth Bandwidth { get; private set; }

    /// <summary>
    /// Activation du filtre de pré-accentuation audio
    /// Améliore la clarté vocale
    /// </summary>
    public bool PreEmph { get; private set; }

    /// <summary>
    /// Activation du filtre passe-haut
    /// Réduit les basses fréquences
    /// </summary>
    public bool HighPass { get; private set; }

    /// <summary>
    /// Activation du filtre passe-bas
    /// Réduit les hautes fréquences
    /// </summary>
    public bool LowPass { get; private set; }

    /// <summary>
    /// Constructeur par défaut requis pour Marten (rehydratation)
    /// </summary>
    public SA818Aggregate()
    {
    }

    /// <summary>
    /// Factory method pour créer le SA818Aggregate avec l'ID fixe et des valeurs par défaut.
    /// Retourne un Validation&lt;Error, SA818Aggregate&gt; selon le Result Pattern.
    /// </summary>
    /// <param name="volume">Volume audio (1-8)</param>
    /// <param name="squelch">Niveau de squelch (0-8)</param>
    /// <param name="bandwidth">Largeur de bande</param>
    /// <param name="preEmph">Activation pré-accentuation</param>
    /// <param name="highPass">Activation filtre passe-haut</param>
    /// <param name="lowPass">Activation filtre passe-bas</param>
    /// <returns>Validation contenant l'aggregate ou les erreurs</returns>
    public static Validation<Error, SA818Aggregate> Create(
        int volume = 4,
        int squelch = 4,
        SA818Bandwidth bandwidth = SA818Bandwidth.Wide25kHz,
        bool preEmph = false,
        bool highPass = false,
        bool lowPass = false)
    {
        // Validations
        var volumeValidation = ValidateVolume(volume);
        var squelchValidation = ValidateSquelch(squelch);

        // Combinaison de toutes les validations
        return (volumeValidation, squelchValidation)
            .Apply((validVolume, validSquelch) =>
            {
                var aggregate = new SA818Aggregate();
                var @event = new SA818ConfigurationUpdatedEvent(
                    FixedId,
                    validVolume,
                    validSquelch,
                    bandwidth,
                    preEmph,
                    highPass,
                    lowPass);

                aggregate.Apply(@event);
                aggregate.AddDomainEvent(@event);

                return aggregate;
            });
    }

    /// <summary>
    /// Met à jour la configuration du SA818.
    /// </summary>
    /// <param name="volume">Volume audio (1-8)</param>
    /// <param name="squelch">Niveau de squelch (0-8)</param>
    /// <param name="bandwidth">Largeur de bande</param>
    /// <param name="preEmph">Activation pré-accentuation</param>
    /// <param name="highPass">Activation filtre passe-haut</param>
    /// <param name="lowPass">Activation filtre passe-bas</param>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> UpdateConfiguration(
        int volume,
        int squelch,
        SA818Bandwidth bandwidth,
        bool preEmph,
        bool highPass,
        bool lowPass)
    {
        // Validations
        var volumeValidation = ValidateVolume(volume);
        var squelchValidation = ValidateSquelch(squelch);

        return (volumeValidation, squelchValidation)
            .Apply((validVolume, validSquelch) =>
            {
                var @event = new SA818ConfigurationUpdatedEvent(
                    Id,
                    validVolume,
                    validSquelch,
                    bandwidth,
                    preEmph,
                    highPass,
                    lowPass);

                Apply(@event);
                AddDomainEvent(@event);

                return unit;
            });
    }

    #region Event Sourcing

    /// <summary>
    /// Applique l'événement SA818ConfigurationUpdatedEvent (Event Sourcing)
    /// </summary>
    public void Apply(SA818ConfigurationUpdatedEvent @event)
    {
        Id = @event.Id;
        Volume = @event.Volume;
        Squelch = @event.Squelch;
        Bandwidth = @event.Bandwidth;
        PreEmph = @event.PreEmph;
        HighPass = @event.HighPass;
        LowPass = @event.LowPass;
    }

    #endregion

    #region Validations

    /// <summary>
    /// Valide le volume audio (plage 1-8)
    /// </summary>
    private static Validation<Error, int> ValidateVolume(int volume)
    {
        if (volume < 1 || volume > 8)
        {
            return Error.Validation(
                "SA818_VOLUME_INVALID",
                "Le volume doit être entre 1 et 8")
                .ToFailure<int>();
        }

        return volume.ToSuccess();
    }

    /// <summary>
    /// Valide le squelch (plage 0-8)
    /// </summary>
    private static Validation<Error, int> ValidateSquelch(int squelch)
    {
        if (squelch < 0 || squelch > 8)
        {
            return Error.Validation(
                "SA818_SQUELCH_INVALID",
                "Le squelch doit être entre 0 et 8")
                .ToFailure<int>();
        }

        return squelch.ToSuccess();
    }

    #endregion
}
