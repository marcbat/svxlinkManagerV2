using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;

/// <summary>
/// Événement émis lors de la mise à jour de la configuration d'un Reflector
/// </summary>
public record ReflectorConfigurationUpdated : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Reflector
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nouveau nom du reflector
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Nouveau contenu brut du fichier de configuration INI svxreflector.conf
    /// </summary>
    public string Config { get; init; } = string.Empty;

    /// <summary>
    /// Constructeur
    /// </summary>
    public ReflectorConfigurationUpdated(Guid id, string name, string config)
    {
        Id = id;
        Name = name;
        Config = config;
    }
}
