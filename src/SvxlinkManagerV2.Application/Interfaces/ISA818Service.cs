using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de communication avec le module SA818 (radio transceiver).
/// </summary>
public interface ISA818Service
{
    /// <summary>
    /// Configure le module SA818 avec un ensemble de commandes AT.
    /// </summary>
    /// <param name="commands">Ensemble des commandes AT à envoyer</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> ConfigureAsync(SA818CommandSet commands, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vérifie si le module SA818 est connecté et répond.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant si le module est connecté</returns>
    Task<Validation<Error, bool>> IsConnectedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Ensemble de commandes AT pour configurer le module SA818.
/// </summary>
/// <param name="DmoSetGroup">Commande AT+DMOSETGROUP (configuration principale)</param>
/// <param name="DmoSetVolume">Commande AT+DMOSETVOLUME (volume audio)</param>
/// <param name="SetFilter">Commande AT+SETFILTER (filtres audio)</param>
public record SA818CommandSet(
    string DmoSetGroup,
    string DmoSetVolume,
    string SetFilter
);
