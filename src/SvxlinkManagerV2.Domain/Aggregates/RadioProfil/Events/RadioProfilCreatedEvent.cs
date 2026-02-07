using SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Events;

/// <summary>
/// Événement émis lors de la création d'un RadioProfil
/// </summary>
public record RadioProfilCreatedEvent : DomainEvent
{
    /// <summary>
    /// Identifiant unique du RadioProfil
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nom du profil radio
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Configuration de réception
    /// </summary>
    public RxConfiguration RxConfiguration { get; init; } = null!;

    /// <summary>
    /// Configuration de transmission
    /// </summary>
    public TxConfiguration TxConfiguration { get; init; } = null!;

    /// <summary>
    /// Constructeur
    /// </summary>
    public RadioProfilCreatedEvent(
        Guid id,
        string name,
        RxConfiguration rxConfiguration,
        TxConfiguration txConfiguration)
    {
        Id = id;
        Name = name;
        RxConfiguration = rxConfiguration;
        TxConfiguration = txConfiguration;
    }
}
