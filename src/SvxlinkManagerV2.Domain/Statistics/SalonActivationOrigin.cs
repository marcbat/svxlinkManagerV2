namespace SvxlinkManagerV2.Domain.Statistics;

/// <summary>
/// Ce qui a déclenché une session : c'est cette information qui dit si le nœud est piloté
/// depuis le navigateur ou depuis la radio.
/// </summary>
public enum SalonActivationOrigin
{
    /// <summary>Action d'un opérateur dans l'interface web.</summary>
    Web = 0,

    /// <summary>Code DTMF de salon composé depuis un transceiver.</summary>
    Dtmf = 1,

    /// <summary>Commande DTMF système de la plage 300-399 (salon par défaut, navigation, déconnexion).</summary>
    SystemCommand = 2,

    /// <summary>Activation automatique au démarrage de l'application.</summary>
    Startup = 3
}
