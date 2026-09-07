using System.Text.Json;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.Reflector;

/// <summary>
/// Lecture du document JSON publié par le serveur HTTP de <c>svxreflector</c>.
/// </summary>
/// <remarks>
/// Document relevé sur la stack Docker le 07/09/2026 :
/// <code>
/// {"nodes":{"HB9GXP3-H":{"isTalker":false,"machineArch":"x86_64",
///  "monitoredTGs":[240,2404],"projVer":"25.05",
///  "protoVer":{"majorVer":3,"minorVer":0},"restrictedTG":true,
///  "sw":"SvxLink","swVer":"1.9.0","tg":0}}}
/// </code>
///
/// L'analyse est délibérément tolérante : le réflecteur ne renseigne un champ que
/// lorsque le nœud le lui a annoncé, et un nœud legacy en annonce très peu. Un champ
/// manquant donne <c>null</c> ou une valeur neutre, jamais un échec — sans quoi la vue
/// de supervision disparaîtrait à cause d'un seul nœud atypique.
/// </remarks>
internal static class ReflectorStatusParser
{
    /// <summary>
    /// Interprète le document de statut.
    /// </summary>
    /// <param name="json">Corps de la réponse HTTP.</param>
    /// <returns>
    /// Les nœuds décrits, ou <c>null</c> si le document n'est pas exploitable —
    /// JSON invalide, ou absence de l'objet <c>nodes</c>.
    /// </returns>
    public static IReadOnlyList<ReflectorNodeStatus>? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("nodes", out var nodes) ||
                nodes.ValueKind != JsonValueKind.Object)
                return null;

            return nodes.EnumerateObject()
                .Where(node => node.Value.ValueKind == JsonValueKind.Object)
                .Select(node => ReadNode(node.Name, node.Value))
                .OrderBy(node => node.Callsign, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ReflectorNodeStatus ReadNode(string callsign, JsonElement node) =>
        new(
            Callsign: callsign,
            TalkGroup: ReadInt(node, "tg") ?? 0,
            MonitoredTalkGroups: ReadIntArray(node, "monitoredTGs"),
            IsTalker: ReadBool(node, "isTalker") ?? false,
            RestrictedTalkGroup: ReadBool(node, "restrictedTG") ?? false,
            Software: ReadString(node, "sw"),
            SoftwareVersion: ReadString(node, "swVer"),
            ProjectVersion: ReadString(node, "projVer"),
            MachineArchitecture: ReadString(node, "machineArch"),
            ProtocolVersion: ReadProtocolVersion(node));

    private static ReflectorProtocolVersion? ReadProtocolVersion(JsonElement node)
    {
        if (!node.TryGetProperty("protoVer", out var version) || version.ValueKind != JsonValueKind.Object)
            return null;

        var major = ReadInt(version, "majorVer");
        if (major is null)
            return null;

        return new ReflectorProtocolVersion(major.Value, ReadInt(version, "minorVer") ?? 0);
    }

    private static string? ReadString(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
                                                 && value.TryGetInt32(out var number)
            ? number
            : null;

    private static bool? ReadBool(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static IReadOnlyList<int> ReadIntArray(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return [];

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out _))
            .Select(item => item.GetInt32())
            .ToList();
    }
}
