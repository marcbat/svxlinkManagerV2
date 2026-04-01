using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de génération du fichier de configuration SVXLink pour le mode Perroquet (Parrot).
/// Génère une configuration SVXLink avec SimplexLogic + ModuleParrot uniquement,
/// sans ReflectorLogic ni LinkToReflector.
/// </summary>
public interface ISvxLinkParrotConfigurationService
{
    /// <summary>
    /// Génère le fichier svxlink.conf pour le mode Perroquet et l'écrit sur le disque.
    /// </summary>
    /// <param name="outputPath">Chemin complet du fichier de sortie (ex: /etc/svxlink/svxlink.conf)</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> GenerateAsync(
        string outputPath,
        CancellationToken cancellationToken = default);
}
