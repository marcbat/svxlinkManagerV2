using SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Events;

/// <summary>
/// Événement émis lors de la mise à jour d'un RadioProfil
/// </summary>
public record RadioProfilUpdatedEvent : DomainEvent
{
    /// <summary>
    /// Identifiant du RadioProfil mis à jour
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nouveau nom du profil (optionnel)
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Nouvelle configuration de réception (optionnel)
    /// </summary>
    public RxConfiguration? RxConfiguration { get; init; }

    /// <summary>
    /// Nouvelle configuration de transmission (optionnel)
    /// </summary>
    public TxConfiguration? TxConfiguration { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public RadioProfilUpdatedEvent(
        Guid id,
        string? name = null,
        RxConfiguration? rxConfiguration = null,
        TxConfiguration? txConfiguration = null)
    {
        Id = id;
        Name = name;
        RxConfiguration = rxConfiguration;
        TxConfiguration = txConfiguration;
    }
}
