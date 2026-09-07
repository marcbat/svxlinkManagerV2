using LanguageExt;
using LanguageExt.Common;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Écriture du document que le nœud publie au réflecteur (<c>NODE_INFO_FILE</c>).
/// </summary>
/// <remarks>
/// Sans lui, le nœud n'est qu'un indicatif anonyme dans les annuaires et tableaux de bord
/// des réflecteurs qui exploitent cette information. L'application connaît pourtant déjà
/// l'essentiel : fréquences et tonalités du salon, indicatif, talkgroup par défaut.
/// </remarks>
public interface INodeInformationWriter
{
    /// <summary>
    /// Écrit le document décrivant le nœud pour le salon donné.
    /// </summary>
    /// <param name="salon">Salon actif, source des fréquences et du talkgroup.</param>
    /// <param name="outputPath">Chemin du fichier à écrire.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, Unit>> WriteAsync(
        SalonAggregate salon,
        string outputPath,
        CancellationToken cancellationToken = default);
}
