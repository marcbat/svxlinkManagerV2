using FluentAssertions;
using SvxlinkManagerV2.Infrastructure.Reflector;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Reflector;

/// <summary>
/// Tests de lecture du document JSON publié par le serveur HTTP de svxreflector.
/// </summary>
/// <remarks>
/// Le document de référence est celui relevé sur la stack Docker le 07/09/2026 : le
/// reproduire ici est ce qui garantit que les noms de champs restent ceux de l'amont.
/// </remarks>
public class ReflectorStatusParserTests
{
    /// <summary>Réponse réelle de svxreflector 25.05, un nœud V3 connecté.</summary>
    private const string RealDocument =
        """
        {"nodes":{"HB9GXP3-H":{"isTalker":false,"machineArch":"x86_64",
        "monitoredTGs":[240,2404],"projVer":"25.05",
        "protoVer":{"majorVer":3,"minorVer":0},"restrictedTG":true,
        "sw":"SvxLink","swVer":"1.9.0","tg":0}}}
        """;

    [Fact]
    public void Parse_WithTheRealDocument_ShouldReadEveryField()
    {
        var nodes = ReflectorStatusParser.Parse(RealDocument);

        nodes.Should().ContainSingle();
        var node = nodes![0];

        node.Callsign.Should().Be("HB9GXP3-H");
        node.TalkGroup.Should().Be(0);
        node.MonitoredTalkGroups.Should().Equal(240, 2404);
        node.IsTalker.Should().BeFalse();
        node.RestrictedTalkGroup.Should().BeTrue();
        node.Software.Should().Be("SvxLink");
        node.SoftwareVersion.Should().Be("1.9.0");
        node.ProjectVersion.Should().Be("25.05");
        node.MachineArchitecture.Should().Be("x86_64");
        node.ProtocolVersion.Should().NotBeNull();
        node.ProtocolVersion!.Major.Should().Be(3);
        node.ProtocolVersion.Minor.Should().Be(0);
        node.ProtocolVersion.ToString().Should().Be("3.0");
    }

    [Fact]
    public void Parse_ShouldOrderNodesByCallsign()
    {
        // L'ordre d'un objet JSON n'est pas une garantie : sans tri, la liste sauterait
        // d'une lecture à l'autre et la vue clignoterait.
        const string json =
            """{"nodes":{"HB9ZZZ-H":{"tg":240},"HB9AAA-H":{"tg":0},"HB9MMM-H":{"tg":2403}}}""";

        var nodes = ReflectorStatusParser.Parse(json);

        nodes!.Select(n => n.Callsign).Should().Equal("HB9AAA-H", "HB9MMM-H", "HB9ZZZ-H");
    }

    [Fact]
    public void Parse_WithATalker_ShouldIdentifyIt()
    {
        const string json =
            """{"nodes":{"HB9AAA-H":{"tg":240,"isTalker":true},"HB9BBB-H":{"tg":240,"isTalker":false}}}""";

        var nodes = ReflectorStatusParser.Parse(json);

        nodes!.Should().ContainSingle(n => n.IsTalker).Which.Callsign.Should().Be("HB9AAA-H");
    }

    /// <summary>
    /// Le réflecteur ne renseigne un champ que si le nœud le lui a annoncé : un nœud legacy
    /// en annonce très peu. Perdre toute la vue à cause de lui serait absurde.
    /// </summary>
    [Fact]
    public void Parse_WithASparseNode_ShouldFallBackOnNeutralValues()
    {
        const string json = """{"nodes":{"HB9GXP2-H":{}}}""";

        var nodes = ReflectorStatusParser.Parse(json);

        var node = nodes!.Should().ContainSingle().Subject;
        node.Callsign.Should().Be("HB9GXP2-H");
        node.TalkGroup.Should().Be(0);
        node.MonitoredTalkGroups.Should().BeEmpty();
        node.IsTalker.Should().BeFalse();
        node.RestrictedTalkGroup.Should().BeFalse();
        node.Software.Should().BeNull();
        node.ProtocolVersion.Should().BeNull();
    }

    [Fact]
    public void Parse_WithAnEmptyNodeList_ShouldReturnAnEmptyList()
    {
        // Un réflecteur qui tourne sans nœud connecté est un cas normal, pas une panne.
        var nodes = ReflectorStatusParser.Parse("""{"nodes":{}}""");

        nodes.Should().NotBeNull();
        nodes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pas du json")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{"autre":1}""")]
    [InlineData("""{"nodes":[]}""")]
    [InlineData("""{"nodes":"HB9AAA-H"}""")]
    public void Parse_WithAnUnusableDocument_ShouldReturnNull(string json)
    {
        // null distingue « document inexploitable » de « aucun nœud connecté » : la page
        // dit alors que le statut est illisible au lieu d'afficher un réflecteur désert.
        ReflectorStatusParser.Parse(json).Should().BeNull();
    }

    [Fact]
    public void Parse_WithMistypedFields_ShouldIgnoreThemRatherThanFail()
    {
        const string json =
            """
            {"nodes":{"HB9AAA-H":{"tg":"240","isTalker":"oui","monitoredTGs":"240",
            "protoVer":3,"sw":42}}}
            """;

        var nodes = ReflectorStatusParser.Parse(json);

        var node = nodes!.Should().ContainSingle().Subject;
        node.TalkGroup.Should().Be(0);
        node.IsTalker.Should().BeFalse();
        node.MonitoredTalkGroups.Should().BeEmpty();
        node.ProtocolVersion.Should().BeNull();
        node.Software.Should().BeNull();
    }

    [Fact]
    public void Parse_WithAPartialProtocolVersion_ShouldDefaultTheMinor()
    {
        const string json = """{"nodes":{"HB9AAA-H":{"protoVer":{"majorVer":2}}}}""";

        var nodes = ReflectorStatusParser.Parse(json);

        nodes![0].ProtocolVersion.Should().Be(new Application.Models.ReflectorProtocolVersion(2, 0));
    }

    [Fact]
    public void Parse_WithANonNumericMonitoredTalkGroup_ShouldKeepTheOthers()
    {
        const string json = """{"nodes":{"HB9AAA-H":{"monitoredTGs":[240,"x",2404]}}}""";

        var nodes = ReflectorStatusParser.Parse(json);

        nodes![0].MonitoredTalkGroups.Should().Equal(240, 2404);
    }
}
