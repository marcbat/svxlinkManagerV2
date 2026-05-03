using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;

/// <summary>
/// Événement émis lors de la création d'un Reflector
/// </summary>
public record ReflectorCreated : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Reflector
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nom du reflector (ex: "SvxReflector Local")
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Contenu brut du fichier de configuration INI svxreflector.conf
    /// </summary>
    public string Config { get; init; } = string.Empty;

    /// <summary>
    /// Constructeur
    /// </summary>
    public ReflectorCreated(Guid id, string name, string config)
    {
        Id = id;
        Name = name;
        Config = config;
    }
}
