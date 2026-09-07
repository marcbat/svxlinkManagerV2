using System.Text.Json.Nodes;
using FluentAssertions;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests du document publié au réflecteur (<c>NODE_INFO_FILE</c>).
/// </summary>
/// <remarks>
/// La structure visée est celle de <c>node_info.json</c>, le modèle de référence livré avec
/// SVXLink : <c>nodeLocation</c>, <c>nodeClass</c>, <c>hidden</c>, <c>sysop</c>,
/// <c>toneToTalkgroup</c>, et un tableau <c>qth</c> portant position, récepteurs et
/// émetteurs. Les noms de champs sont ceux du modèle, pas les nôtres : un annuaire qui lit
/// ce document ne connaît que ceux-là.
/// </remarks>
public class NodeInformationWriterTests
{
    #region Identité du nœud

    [Fact]
    public void Build_ShouldPublishTheDeclaredIdentity()
    {
        var information = new NodeInformation(
            Location: "Genève",
            Class: NodeClass.Repeater,
            Sysop: "HB9GXP HB9ABC");

        var document = NodeInformationWriter.Build(CreateSalon(), information);

        document["nodeLocation"]!.GetValue<string>().Should().Be("Genève");
        document["nodeClass"]!.GetValue<string>().Should().Be("repeater");
        document["sysop"]!.GetValue<string>().Should().Be("HB9GXP HB9ABC");
    }

    [Theory]
    [InlineData(NodeClass.Simplex, "simplex")]
    [InlineData(NodeClass.Repeater, "repeater")]
    [InlineData(NodeClass.Hotspot, "hotspot")]
    [InlineData(NodeClass.Bridge, "bridge")]
    public void Build_ShouldUseTheUpstreamNamesForTheNodeClass(NodeClass nodeClass, string expected)
    {
        // Les traduire les rendrait incompréhensibles aux annuaires qui lisent ce document.
        var document = NodeInformationWriter.Build(CreateSalon(), new NodeInformation(Class: nodeClass));

        document["nodeClass"]!.GetValue<string>().Should().Be(expected);
    }

    [Fact]
    public void Build_WithNothingDeclared_ShouldOmitTheEmptyFields()
    {
        var document = NodeInformationWriter.Build(CreateSalon(), NodeInformation.Empty);

        document.ContainsKey("nodeLocation").Should().BeFalse();
        document.ContainsKey("sysop").Should().BeFalse();
        document["nodeClass"]!.GetValue<string>().Should().Be("simplex");
    }

    /// <summary>
    /// Le nœud masqué est publié avec <c>hidden</c> à vrai plutôt que passé sous silence :
    /// c'est ce que prévoit le format, et cela laisse au réflecteur le soin de respecter la
    /// demande.
    /// </summary>
    [Fact]
    public void Build_WithAHiddenNode_ShouldSaySoRatherThanPublishNothing()
    {
        var document = NodeInformationWriter.Build(CreateSalon(), new NodeInformation(Hidden: true));

        document["hidden"]!.GetValue<bool>().Should().BeTrue();
    }

    #endregion

    #region Position

    [Fact]
    public void Build_WithCoordinates_ShouldPublishThem()
    {
        var information = new NodeInformation(Latitude: 46.20222, Longitude: 6.14569, Locator: "JN36BF");

        var position = NodeInformationWriter.Build(CreateSalon(), information)["qth"]![0]!["pos"]!;

        position["lat"]!.GetValue<double>().Should().BeApproximately(46.20222, 0.00001);
        position["long"]!.GetValue<double>().Should().BeApproximately(6.14569, 0.00001);
        position["loc"]!.GetValue<string>().Should().Be("JN36BF");
    }

    [Fact]
    public void Build_WithOnlyALocator_ShouldPublishItWithoutCoordinates()
    {
        var information = new NodeInformation(Locator: "JN36BF");

        var position = NodeInformationWriter.Build(CreateSalon(), information)["qth"]![0]!["pos"]!;

        position["loc"]!.GetValue<string>().Should().Be("JN36BF");
        position.AsObject().ContainsKey("lat").Should().BeFalse();
    }

    [Fact]
    public void Build_WithoutAnyPosition_ShouldOmitIt()
    {
        var qth = NodeInformationWriter.Build(CreateSalon(), NodeInformation.Empty)["qth"]![0]!.AsObject();

        qth.ContainsKey("pos").Should().BeFalse();
    }

    #endregion

    #region Radio

    [Fact]
    public void Build_ShouldPublishTheRealFrequenciesOfTheSalon()
    {
        var salon = CreateSalon(rxFrequency: 438.750m, txFrequency: 431.150m);

        var qth = NodeInformationWriter.Build(salon, NodeInformation.Empty)["qth"]![0]!;

        qth["rx"]!["A"]!["freq"]!.GetValue<double>().Should().Be(438.750);
        qth["tx"]!["A"]!["freq"]!.GetValue<double>().Should().Be(431.150);
    }

    /// <summary>
    /// Le format attend un tableau de tonalités en réception et une valeur unique en
    /// émission : l'asymétrie vient du modèle de référence, pas de nous.
    /// </summary>
    [Fact]
    public void Build_ShouldPublishTheCtcssTonesInTheExpectedShape()
    {
        var salon = CreateSalon(rxCtcss: 136.5m, txCtcss: 123.0m);

        var qth = NodeInformationWriter.Build(salon, NodeInformation.Empty)["qth"]![0]!;

        qth["rx"]!["A"]!["ctcssFreq"]!.AsArray()[0]!.GetValue<double>().Should().Be(136.5);
        qth["tx"]!["A"]!["ctcssFreq"]!.GetValue<double>().Should().Be(123.0);
    }

    [Fact]
    public void Build_WithACtcssReceiver_ShouldDeclareTheSquelchType()
    {
        var withTone = NodeInformationWriter.Build(CreateSalon(rxCtcss: 136.5m), NodeInformation.Empty);
        var withoutTone = NodeInformationWriter.Build(CreateSalon(rxCtcss: null), NodeInformation.Empty);

        withTone["qth"]![0]!["rx"]!["A"]!["sqlType"]!.GetValue<string>().Should().Be("CTCSS");
        withoutTone["qth"]![0]!["rx"]!["A"]!["sqlType"]!.GetValue<string>().Should().Be("COS");
    }

    [Fact]
    public void Build_ShouldNotInventAntennasOrPower()
    {
        // Le modèle de référence les décrit, mais rien dans la configuration ne les
        // modélise : mieux vaut les omettre que publier des valeurs inventées.
        var qth = NodeInformationWriter.Build(CreateSalon(), NodeInformation.Empty)["qth"]![0]!;

        qth["rx"]!["A"]!.AsObject().ContainsKey("ant").Should().BeFalse();
        qth["tx"]!["A"]!.AsObject().ContainsKey("ant").Should().BeFalse();
        qth["tx"]!["A"]!.AsObject().ContainsKey("pwr").Should().BeFalse();
    }

    #endregion

    #region Tonalité et talkgroup

    [Fact]
    public void Build_ShouldMapTheReceivedToneToTheDefaultTalkGroup()
    {
        var salon = CreateSalon(rxCtcss: 136.5m, defaultTg: 2403);

        var map = NodeInformationWriter.Build(salon, NodeInformation.Empty)["toneToTalkgroup"]!.AsObject();

        map["136.5"]!.GetValue<int>().Should().Be(2403);
    }

    [Fact]
    public void Build_WithoutAnyReceivedTone_ShouldOmitTheMapping()
    {
        // Sans CTCSS en réception, associer une tonalité à un talkgroup n'a pas de sens.
        var document = NodeInformationWriter.Build(CreateSalon(rxCtcss: null), NodeInformation.Empty);

        document.ContainsKey("toneToTalkgroup").Should().BeFalse();
    }

    [Fact]
    public void Build_WithAV2Salon_ShouldMapTheToneToNoTalkGroup()
    {
        // SVXLink 19.09.2 ignore les talkgroups : publier celui du salon serait mensonger.
        var salon = CreateSalon(rxCtcss: 136.5m, defaultTg: 2403, protocol: ReflectorProtocol.V2);

        var map = NodeInformationWriter.Build(salon, NodeInformation.Empty)["toneToTalkgroup"]!.AsObject();

        map["136.5"]!.GetValue<int>().Should().Be(0);
    }

    #endregion

    [Fact]
    public void Build_ShouldProduceADocumentShapedLikeTheReference()
    {
        var document = NodeInformationWriter.Build(CreateSalon(), NodeInformation.Empty);

        document["qth"].Should().BeOfType<JsonArray>();
        document["qth"]!.AsArray().Should().ContainSingle();
        document["qth"]![0]!["rx"]!.AsObject().ContainsKey("A").Should().BeTrue();
        document["qth"]![0]!["tx"]!.AsObject().ContainsKey("A").Should().BeTrue();
    }

    private static SalonAggregate CreateSalon(
        decimal rxFrequency = 145.550m,
        decimal txFrequency = 145.550m,
        decimal? rxCtcss = 136.5m,
        decimal? txCtcss = 136.5m,
        int defaultTg = 0,
        ReflectorProtocol protocol = ReflectorProtocol.V3)
    {
        var config = new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d",
            16000,
            1,
            "ref.f5kri.fr",
            5300,
            "HB9GXP-H",
            protocol == ReflectorProtocol.V2 ? "test-auth-key" : null,
            0,
            protocol,
            null,
            "HB9GXP",
            "ModuleHelp",
            60,
            60,
            null,
            "fr_FR",
            0,
            rxFrequency,
            txFrequency,
            rxCtcss,
            txCtcss,
            DefaultTg: defaultTg);

        return SalonAggregate.Create(Guid.NewGuid(), "Salon Test", false, config).Match(
            Succ: aggregate => aggregate,
            Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));
    }
}
