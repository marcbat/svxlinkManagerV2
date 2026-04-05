using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service d'écriture de commandes DTMF dans le PTY SVXLink.
/// Permet à l'application .NET de déclencher des commandes internes SVXLink
/// via le pseudo-terminal DTMF_CTRL_PTY.
/// </summary>
public interface IDtmfPtyWriter
{
    /// <summary>
    /// Envoie une commande DTMF dans le PTY SVXLink sous la forme "{cmd}#".
    /// </summary>
    /// <param name="command">Code DTMF à envoyer (ex : "399").</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Unit en cas de succès, ou une erreur.</returns>
    Task<Validation<Error, Unit>> SendCommandAsync(string command, CancellationToken cancellationToken = default);
}
