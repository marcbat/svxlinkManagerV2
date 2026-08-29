namespace SvxlinkManagerV2.Domain.Aggregates.Salon;

/// <summary>
/// Décrit une commande DTMF système de la plage réservée aux annonces (300-399).
/// </summary>
/// <param name="Code">Code DTMF à composer depuis le transceiver.</param>
/// <param name="Description">Description en français de l'action déclenchée.</param>
public record DtmfSystemCommand(int Code, string Description);

/// <summary>
/// Catalogue des commandes DTMF système (pilotage du nœud par radio, sans interface web).
/// Ces codes appartiennent à la plage réservée 300-399 (<see cref="DtmfCodeRanges"/>) et ne
/// peuvent donc pas être attribués à un salon.
///
/// Source unique de vérité : utilisée à la fois par le routage
/// (<c>DtmfSystemCommandService</c>) et par la page d'aide.
/// </summary>
public static class DtmfSystemCommands
{
    /// <summary>Retour au salon par défaut.</summary>
    public const int DefaultSalon = 310;

    /// <summary>Déconnexion du salon actif (bascule en mode autonome).</summary>
    public const int Disconnect = 311;

    /// <summary>Salon suivant, par ordre de code DTMF.</summary>
    public const int NextSalon = 312;

    /// <summary>Salon précédent, par ordre de code DTMF.</summary>
    public const int PreviousSalon = 313;

    /// <summary>Redémarrage du daemon SVXLink.</summary>
    public const int RestartDaemon = 320;

    /// <summary>
    /// Liste des commandes système exposées aux opérateurs, ordonnée par code.
    /// </summary>
    public static IReadOnlyList<DtmfSystemCommand> All { get; } =
    [
        new(DefaultSalon, "Revenir au salon par défaut"),
        new(Disconnect, "Se déconnecter du salon actif (mode autonome)"),
        new(NextSalon, "Passer au salon suivant (par ordre de code DTMF)"),
        new(PreviousSalon, "Passer au salon précédent (par ordre de code DTMF)"),
        new(RestartDaemon, "Redémarrer SVXLink en conservant le salon actif")
    ];

    /// <summary>
    /// Indique si le code DTMF correspond à une commande système.
    /// </summary>
    public static bool IsSystemCommand(int code) => All.Any(c => c.Code == code);
}
