using LanguageExt;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Lecture du fichier svxlink.conf effectivement déployé sur la machine, tel que généré
/// lors de la dernière activation de salon. Complète <see cref="ISvxLinkConfigurationService"/>,
/// qui n'expose que la génération.
/// </summary>
public interface ISvxLinkConfigurationReader
{
    /// <summary>
    /// Chemin du fichier de configuration interrogé.
    /// </summary>
    string ConfigurationPath { get; }

    /// <summary>
    /// Lit le contenu brut du fichier de configuration.
    /// Retourne un échec si le fichier est absent ou illisible : l'appelant reste libre
    /// de poursuivre sans la configuration.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, string>> ReadAsync(CancellationToken cancellationToken = default);
}
