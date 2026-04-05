using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Fournisseur d'information associé à une commande DTMF spécifique (plage 301–398).
/// Chaque implémentation est responsable d'un code DTMF donné et retourne
/// une phrase en français décrivant l'information correspondante.
/// </summary>
public interface IInfoProvider
{
    /// <summary>
    /// Code DTMF associé à ce fournisseur (ex : 301 pour la température CPU).
    /// </summary>
    int DtmfCode { get; }

    /// <summary>
    /// Description courte de l'information fournie (à des fins de logging).
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Récupère le texte d'information à synthétiser.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Texte en français prêt à être synthétisé, ou une erreur.</returns>
    Task<Validation<Error, string>> GetInfoTextAsync(CancellationToken cancellationToken = default);
}
