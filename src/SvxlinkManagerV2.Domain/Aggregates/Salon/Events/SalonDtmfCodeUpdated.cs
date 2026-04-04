using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

/// <summary>
/// Événement émis lors de la mise à jour du code DTMF d'un Salon.
/// Le code DTMF permet de changer de salon via une commande radio.
/// </summary>
public record SalonDtmfCodeUpdated : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Salon
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Code DTMF associé au salon (null = pas de code DTMF)
    /// </summary>
    public int? DtmfCode { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SalonDtmfCodeUpdated(Guid id, int? dtmfCode)
    {
        Id = id;
        DtmfCode = dtmfCode;
    }
}
