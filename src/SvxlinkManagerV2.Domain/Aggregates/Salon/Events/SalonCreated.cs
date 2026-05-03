using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

/// <summary>
/// Événement émis lors de la création d'un Salon
/// </summary>
public record SalonCreated : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Salon
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nom du salon (ex: "Salon National France")
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Indique si c'est le salon par défaut (activé au démarrage)
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Configuration complète SVXLink pour ce salon
    /// </summary>
    public SvxLinkConfiguration Configuration { get; init; } = null!;

    /// <summary>
    /// Type de salon (Reflector ou Parrot)
    /// </summary>
    public SalonType SalonType { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SalonCreated(
        Guid id,
        string name,
        bool isDefault,
        SvxLinkConfiguration configuration,
        SalonType salonType = SalonType.Reflector)
    {
        Id = id;
        Name = name;
        IsDefault = isDefault;
        Configuration = configuration;
        SalonType = salonType;
    }
}
