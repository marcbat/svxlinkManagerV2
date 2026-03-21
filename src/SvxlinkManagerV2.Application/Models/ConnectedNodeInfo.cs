namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// Représente un nœud connecté au réflecteur SVXLink.
/// </summary>
/// <param name="Name">Le callsign du nœud (ex: "HB9GXP-H")</param>
public record ConnectedNodeInfo(string Name);
