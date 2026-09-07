namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// État du certificat X.509 du nœud (protocole V3).
/// </summary>
public enum NodeCertificateStatus
{
    /// <summary>Le salon actif n'utilise pas le protocole V3 : la notion n'existe pas.</summary>
    NotApplicable,

    /// <summary>
    /// Aucun fichier dans la PKI : le daemon n'a pas encore démarré en V3, ou n'a pas eu le
    /// temps de générer sa clé.
    /// </summary>
    NotGenerated,

    /// <summary>
    /// Clé et demande générées, aucun certificat : le sysop du réflecteur n'a pas encore
    /// signé. <b>Ce n'est pas une panne</b> — l'attente peut durer des heures ou des jours.
    /// </summary>
    PendingSignature,

    /// <summary>Certificat valide.</summary>
    Valid,

    /// <summary>Certificat valide mais proche de son échéance.</summary>
    Expiring,

    /// <summary>Certificat expiré : le réflecteur le refusera.</summary>
    Expired,

    /// <summary>Un certificat est présent mais illisible.</summary>
    Unreadable
}

/// <summary>
/// Instantané du cycle de vie du certificat du nœud.
/// </summary>
/// <param name="Status">Étape du cycle de vie.</param>
/// <param name="Callsign">Indicatif attendu, celui du salon actif.</param>
/// <param name="Subject">Sujet du certificat, <c>null</c> tant qu'il n'existe pas.</param>
/// <param name="Issuer">Autorité émettrice.</param>
/// <param name="NotBefore">Début de validité, en heure locale.</param>
/// <param name="NotAfter">Fin de validité, en heure locale.</param>
/// <param name="RequestedAt">Date de dépôt de la demande, quand elle est en attente.</param>
/// <remarks>
/// La clé privée du nœud n'apparaît pas ici, et n'est jamais lue : sa seule présence sur le
/// disque suffit à l'application, et rien de ce modèle ne doit pouvoir la faire fuir vers une
/// page web.
/// </remarks>
public record NodeCertificateState(
    NodeCertificateStatus Status,
    string? Callsign = null,
    string? Subject = null,
    string? Issuer = null,
    DateTime? NotBefore = null,
    DateTime? NotAfter = null,
    DateTime? RequestedAt = null)
{
    /// <summary>
    /// Marge avant l'échéance à partir de laquelle l'interface alerte.
    /// </summary>
    /// <remarks>
    /// Trente jours parce que le remède dépend d'un tiers : sur un réflecteur public il faut
    /// laisser à un sysop bénévole le temps de voir passer la demande.
    /// </remarks>
    public const int ExpiryWarningDays = 30;

    /// <summary>Le salon actif n'utilise pas le protocole V3.</summary>
    public static readonly NodeCertificateState NotApplicable = new(NodeCertificateStatus.NotApplicable);

    /// <summary>Jours restants avant l'échéance, négatif si le certificat est expiré.</summary>
    public int? DaysUntilExpiry =>
        NotAfter is { } expiry ? (int)Math.Floor((expiry - DateTime.Now).TotalDays) : null;

    /// <summary>L'état demande une action ou une attention de l'opérateur.</summary>
    public bool NeedsAttention =>
        Status is NodeCertificateStatus.Expiring or NodeCertificateStatus.Expired
            or NodeCertificateStatus.Unreadable;
}
