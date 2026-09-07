using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Envoi de commandes runtime au démon <c>svxreflector</c> via son pseudo-terminal
/// <c>COMMAND_PTY</c>.
/// </summary>
/// <remarks>
/// Même mécanique que <see cref="IDtmfPtyWriter"/> côté SVXLink : le démon crée le PTY et lit
/// ce qu'on y écrit. C'est un canal de contrôle <b>non authentifié</b> — mais un fichier
/// local, pas un port réseau : sa protection est celle du système de fichiers.
///
/// Le PTY est en écriture seule de notre point de vue : les réponses du démon partent dans
/// son journal, pas vers l'écrivain. Tout ce qui doit être <em>lu</em> passe donc par
/// ailleurs — le système de fichiers pour les demandes en attente, l'API HTTP pour les nœuds.
/// </remarks>
public interface IReflectorCommandWriter
{
    /// <summary>
    /// Écrit une commande dans le PTY du réflecteur.
    /// </summary>
    /// <param name="command">Commande sans saut de ligne (ex : <c>CA SIGN HB9GXP-H</c>).</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, Unit>> SendCommandAsync(string command, CancellationToken cancellationToken = default);
}
