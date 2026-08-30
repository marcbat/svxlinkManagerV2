namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// État de la liaison logique entre le nœud et le réflecteur.
/// Distinct de l'état du processus svxlink : le daemon peut tourner
/// alors que la liaison est refusée (clé erronée, hôte injoignable, certificat rejeté).
/// </summary>
public enum ReflectorLinkStatus
{
    /// <summary>Aucune liaison attendue : le daemon n'a pas encore été démarré.</summary>
    Inactive,

    /// <summary>Salon autonome (perroquet) ou mode simplex : la configuration ne comporte pas de ReflectorLogic.</summary>
    NotApplicable,

    /// <summary>Tentative de connexion en cours (TCP, TLS ou authentification).</summary>
    Connecting,

    /// <summary>Liaison établie : le réflecteur a transmis la liste des nœuds.</summary>
    Connected,

    /// <summary>Liaison perdue après avoir été établie.</summary>
    Disconnected,

    /// <summary>La liaison n'a pas pu être établie.</summary>
    Failed
}

/// <summary>
/// Cause d'un échec ou d'une perte de liaison, déduite des logs SVXLink.
/// </summary>
public enum ReflectorLinkFailureReason
{
    /// <summary>Aucune cause : la liaison est nominale.</summary>
    None,

    /// <summary>Le réflecteur a refusé l'authentification (AUTH_KEY, indicatif inconnu).</summary>
    AuthenticationRejected,

    /// <summary>L'hôte du réflecteur est injoignable (DNS, connexion refusée, timeout).</summary>
    HostUnreachable,

    /// <summary>Le certificat X.509 du protocole V3 a été rejeté ou n'a pas pu être chargé.</summary>
    CertificateRejected,

    /// <summary>Erreur de protocole entre le nœud et le réflecteur (versions incompatibles).</summary>
    ProtocolError,

    /// <summary>Le réflecteur a fermé la connexion.</summary>
    RemoteDisconnected,

    /// <summary>Plus aucun battement de cœur reçu : la liaison est considérée comme perdue.</summary>
    HeartbeatTimeout,

    /// <summary>La configuration de la section ReflectorLogic est incomplète.</summary>
    ConfigurationInvalid,

    /// <summary>Cause non reconnue.</summary>
    Unknown
}

/// <summary>
/// Instantané de l'état de la liaison au réflecteur.
/// </summary>
/// <param name="Status">État courant de la liaison.</param>
/// <param name="Reason">Cause de l'échec ou de la perte, <see cref="ReflectorLinkFailureReason.None"/> si la liaison est nominale.</param>
/// <param name="Detail">Ligne de log SVXLink à l'origine de l'état, pour affichage à l'utilisateur.</param>
public record ReflectorLinkState(
    ReflectorLinkStatus Status,
    ReflectorLinkFailureReason Reason = ReflectorLinkFailureReason.None,
    string? Detail = null)
{
    /// <summary>Aucune liaison attendue tant que le daemon n'a pas été démarré.</summary>
    public static readonly ReflectorLinkState Inactive = new(ReflectorLinkStatus.Inactive);

    /// <summary>Mode autonome : aucune liaison réflecteur n'est configurée.</summary>
    public static readonly ReflectorLinkState NotApplicable = new(ReflectorLinkStatus.NotApplicable);

    /// <summary>Indique que la liaison est en échec ou perdue.</summary>
    public bool IsFaulted => Status is ReflectorLinkStatus.Failed or ReflectorLinkStatus.Disconnected;
}
