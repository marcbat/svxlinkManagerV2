using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.SA818.Events;

/// <summary>
/// Événement émis lorsque la configuration du module SA818 est mise à jour.
/// Contient tous les paramètres hardware globaux du SA818.
/// </summary>
public record SA818ConfigurationUpdatedEvent : DomainEvent
{
    /// <summary>
    /// Identifiant unique du SA818 (ID fixe : 00000000-0000-0000-0000-000000000001)
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Volume audio (plage valide: 1-8)
    /// </summary>
    public int Volume { get; init; }

    /// <summary>
    /// Niveau de squelch (plage valide: 0-8)
    /// </summary>
    public int Squelch { get; init; }

    /// <summary>
    /// Largeur de bande (12.5kHz ou 25kHz)
    /// </summary>
    public SA818Bandwidth Bandwidth { get; init; }

    /// <summary>
    /// Activation du filtre de pré-accentuation audio
    /// </summary>
    public bool PreEmph { get; init; }

    /// <summary>
    /// Activation du filtre passe-haut
    /// </summary>
    public bool HighPass { get; init; }

    /// <summary>
    /// Activation du filtre passe-bas
    /// </summary>
    public bool LowPass { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SA818ConfigurationUpdatedEvent(
        Guid id,
        int volume,
        int squelch,
        SA818Bandwidth bandwidth,
        bool preEmph,
        bool highPass,
        bool lowPass)
    {
        Id = id;
        Volume = volume;
        Squelch = squelch;
        Bandwidth = bandwidth;
        PreEmph = preEmph;
        HighPass = highPass;
        LowPass = lowPass;
    }
}
