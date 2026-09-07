namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// Disponibilité de l'API de statut HTTP du réflecteur local.
/// </summary>
/// <remarks>
/// Le réflecteur arrêté ou l'API non configurée sont des situations normales, pas des
/// erreurs : la page de supervision doit les nommer plutôt que tomber.
/// </remarks>
public enum ReflectorStatusAvailability
{
    /// <summary>Aucune interrogation n'a encore eu lieu.</summary>
    Unknown,

    /// <summary>Statut lu et interprété.</summary>
    Available,

    /// <summary>Le démon <c>svxreflector</c> ne tourne pas.</summary>
    DaemonStopped,

    /// <summary>La configuration du réflecteur ne déclare pas <c>HTTP_SRV_PORT</c>.</summary>
    PortNotConfigured,

    /// <summary>Le port est déclaré mais ne répond pas.</summary>
    Unreachable,

    /// <summary>La réponse n'est pas le document JSON attendu.</summary>
    Invalid
}

/// <summary>
/// Version du protocole réflecteur annoncée par un nœud.
/// </summary>
/// <param name="Major">Version majeure : 3 pour le protocole V3, antérieure pour un nœud legacy.</param>
/// <param name="Minor">Version mineure.</param>
public record ReflectorProtocolVersion(int Major, int Minor)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Major}.{Minor}";
}

/// <summary>
/// État d'un nœud connecté au réflecteur, tel que publié par son API de statut.
/// </summary>
/// <param name="Callsign">Indicatif du nœud, clé de l'objet <c>nodes</c>.</param>
/// <param name="TalkGroup">Talkgroup courant du nœud. <c>0</c> signifie « aucun ».</param>
/// <param name="MonitoredTalkGroups">Talkgroups qu'il surveille en plus du sien.</param>
/// <param name="IsTalker">Le nœud émet en ce moment.</param>
/// <param name="RestrictedTalkGroup">Le nœud est restreint à un talkgroup imposé par le serveur.</param>
/// <param name="Software">Logiciel annoncé (<c>SvxLink</c> pour un nœud standard).</param>
/// <param name="SoftwareVersion">Version du logiciel du nœud.</param>
/// <param name="ProjectVersion">Version du projet SVXLink (ex. <c>25.05</c>).</param>
/// <param name="MachineArchitecture">Architecture de la machine du nœud.</param>
/// <param name="ProtocolVersion">Version du protocole négociée, <c>null</c> si non annoncée.</param>
public record ReflectorNodeStatus(
    string Callsign,
    int TalkGroup,
    IReadOnlyList<int> MonitoredTalkGroups,
    bool IsTalker,
    bool RestrictedTalkGroup,
    string? Software,
    string? SoftwareVersion,
    string? ProjectVersion,
    string? MachineArchitecture,
    ReflectorProtocolVersion? ProtocolVersion);

/// <summary>
/// Instantané du statut du réflecteur local.
/// </summary>
/// <param name="Availability">Ce que vaut cet instantané.</param>
/// <param name="Nodes">Nœuds connectés, vide dès que <paramref name="Availability"/> n'est pas <see cref="ReflectorStatusAvailability.Available"/>.</param>
/// <param name="RetrievedAtUtc">Horodatage de la dernière lecture réussie.</param>
/// <param name="Detail">Précision destinée à l'utilisateur quand le statut n'est pas disponible.</param>
public record ReflectorStatusSnapshot(
    ReflectorStatusAvailability Availability,
    IReadOnlyList<ReflectorNodeStatus> Nodes,
    DateTime? RetrievedAtUtc = null,
    string? Detail = null)
{
    /// <summary>Aucune interrogation n'a encore eu lieu.</summary>
    public static readonly ReflectorStatusSnapshot Unknown =
        new(ReflectorStatusAvailability.Unknown, []);

    /// <summary>Instantané indisponible, avec la raison à afficher.</summary>
    public static ReflectorStatusSnapshot Unavailable(ReflectorStatusAvailability availability, string detail) =>
        new(availability, [], null, detail);

    /// <summary>Le statut est exploitable.</summary>
    public bool IsAvailable => Availability == ReflectorStatusAvailability.Available;

    /// <summary>Indicatif du nœud en émission, s'il y en a un.</summary>
    public string? TalkerCallsign => Nodes.FirstOrDefault(n => n.IsTalker)?.Callsign;
}
