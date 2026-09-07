namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// Représente un nœud connecté au réflecteur SVXLink.
/// </summary>
/// <param name="Name">Le callsign du nœud (ex: "HB9GXP-H")</param>
/// <param name="IsTx">Indique si le nœud est actuellement en émission (TX). Faux par défaut.</param>
/// <param name="TalkGroup">
/// Talkgroup du nœud, <c>null</c> quand il est inconnu.
///
/// Les logs de <c>ReflectorLogic</c> ne le portent pas : ils ne donnent qu'une liste plate
/// d'indicatifs. Cette information vient de l'API de statut du réflecteur local, donc reste
/// nulle pour un réflecteur distant — auquel cas l'affichage groupé n'a pas lieu d'être.
/// </param>
public record ConnectedNodeInfo(string Name, bool IsTx = false, int? TalkGroup = null);
