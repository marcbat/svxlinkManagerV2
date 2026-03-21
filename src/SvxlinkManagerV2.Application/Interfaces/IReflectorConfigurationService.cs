using LanguageExt;
using LanguageExt.Common;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service d'écriture du fichier de configuration svxreflector.conf.
/// Écrit le contenu brut INI stocké dans l'aggregate sur le disque.
/// </summary>
public interface IReflectorConfigurationService
{
    /// <summary>
    /// Écrit le fichier de configuration svxreflector.conf à partir du contenu de l'aggregate.
    /// L'écriture est atomique (fichier temporaire + renommage).
    /// </summary>
    /// <param name="reflector">Aggregate Reflector contenant le texte de configuration</param>
    /// <param name="outputPath">Chemin complet du fichier de sortie (ex: /etc/svxlink/svxreflector.conf)</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> WriteConfigAsync(
        ReflectorAggregate reflector,
        string outputPath,
        CancellationToken cancellationToken = default);
}
