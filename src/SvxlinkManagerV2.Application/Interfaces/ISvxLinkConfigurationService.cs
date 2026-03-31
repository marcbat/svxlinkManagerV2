using LanguageExt;
using LanguageExt.Common;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de génération du fichier de configuration SVXLink (svxlink.conf).
/// Génère le fichier à partir d'un template et des données d'un SalonAggregate.
/// </summary>
public interface ISvxLinkConfigurationService
{
    /// <summary>
    /// Génère le fichier svxlink.conf à partir d'un Salon et l'écrit sur le disque.
    /// </summary>
    /// <param name="salon">Aggregate Salon contenant la configuration complète</param>
    /// <param name="outputPath">Chemin complet du fichier de sortie (ex: /etc/svxlink/svxlink.conf)</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> GenerateAsync(
        SalonAggregate salon, 
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide la syntaxe d'un fichier svxlink.conf existant.
    /// </summary>
    /// <param name="configPath">Chemin du fichier à valider</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant si le fichier est valide</returns>
    Task<Validation<Error, bool>> ValidateAsync(
        string configPath, 
        CancellationToken cancellationToken = default);
}
