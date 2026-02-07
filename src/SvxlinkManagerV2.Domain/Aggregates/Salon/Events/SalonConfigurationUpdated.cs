using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

/// <summary>
/// Événement émis lors de la mise à jour de la configuration d'un Salon
/// </summary>
public record SalonConfigurationUpdated : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Salon
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nouvelle configuration complète SVXLink
    /// </summary>
    public SvxLinkConfiguration Configuration { get; init; } = null!;

    /// <summary>
    /// Constructeur
    /// </summary>
    public SalonConfigurationUpdated(
        Guid id,
        SvxLinkConfiguration configuration)
    {
        Id = id;
        Configuration = configuration;
    }
}
