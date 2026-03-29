namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// Représente un nœud connecté au réflecteur SVXLink.
/// </summary>
/// <param name="Name">Le callsign du nœud (ex: "HB9GXP-H")</param>
/// <param name="IsTx">Indique si le nœud est actuellement en émission (TX). Faux par défaut.</param>
public record ConnectedNodeInfo(string Name, bool IsTx = false);
