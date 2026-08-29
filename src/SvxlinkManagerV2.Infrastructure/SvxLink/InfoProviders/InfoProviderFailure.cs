using LanguageExt;
using Microsoft.Extensions.Logging;
using DomainError = SvxlinkManagerV2.Domain.Common.Error;
using LangError = LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

/// <summary>
/// Conversion des erreurs métier de lecture de métrique vers le type d'erreur
/// attendu par <see cref="Application.Interfaces.IInfoProvider"/>.
/// </summary>
internal static class InfoProviderFailure
{
    /// <summary>
    /// Journalise l'indisponibilité d'une métrique et produit l'échec correspondant.
    /// </summary>
    internal static Validation<LangError, string> From(
        Seq<DomainError> errors,
        ILogger logger,
        string description)
    {
        var message = string.Join(" | ", errors.Select(e => e.Message));
        logger.LogWarning("Information « {Description} » indisponible : {Message}", description, message);

        return Validation<LangError, string>.Fail(
            LanguageExt.Prelude.Seq1(LangError.New(message)));
    }
}
