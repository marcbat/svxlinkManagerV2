using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

public class ConnectedNodesTrackerTests
{
    private readonly ILogger<ConnectedNodesTracker> _logger;
    private readonly ISvxLinkLogService _logService;
    private readonly IReflectorStatusService _statusService;

    public ConnectedNodesTrackerTests()
    {
        _logger = Substitute.For<ILogger<ConnectedNodesTracker>>();
        _logService = Substitute.For<ISvxLinkLogService>();
        _statusService = Substitute.For<IReflectorStatusService>();

        // Par défaut le statut du réflecteur est inconnu : le talkgroup des nœuds reste nul,
        // comme sur un réflecteur distant.
        _statusService.Current.Returns(ReflectorStatusSnapshot.Unknown);
    }

    [Fact]
    public void Constructor_ShouldSubscribeToLogService()
    {
        // Arrange & Act
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        // Assert
        _logService.Received(1).OnLogReceived += Arg.Any<Action<SvxLinkLogEntry>>();
    }

    [Fact]
    public void ConnectedNodes_Initially_ShouldBeEmpty()
    {
        // Arrange & Act
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        // Assert
        tracker.ConnectedNodes.Should().BeEmpty();
    }

    [Fact]
    public void ProcessConnectedNodesLine_ShouldInitializeNodesList()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var nodesInitialized = false;
        IReadOnlyList<ConnectedNodeInfo>? capturedNodes = null;

        tracker.OnNodesInitialized += nodes =>
        {
            nodesInitialized = true;
            capturedNodes = nodes;
        };

        var logEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Connected nodes: HB9GXP2-H, HB9GXP-H",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(logEntry);

        // Assert
        nodesInitialized.Should().BeTrue();
        capturedNodes.Should().NotBeNull();
        capturedNodes!.Should().HaveCount(2);
        capturedNodes.Should().Contain(n => n.Name == "HB9GXP2-H");
        capturedNodes.Should().Contain(n => n.Name == "HB9GXP-H");
        tracker.ConnectedNodes.Should().HaveCount(2);
    }

    [Fact]
    public void ProcessConnectedNodesLine_WithSingleNode_ShouldInitializeCorrectly()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var logEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Connected nodes: F5ABC-L",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(logEntry);

        // Assert
        tracker.ConnectedNodes.Should().HaveCount(1);
        tracker.ConnectedNodes[0].Name.Should().Be("F5ABC-L");
    }

    [Fact]
    public void ProcessConnectedNodesLine_ShouldClearPreviousNodes()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        var firstEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Connected nodes: NODE1, NODE2",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(firstEntry);

        var secondEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Connected nodes: NODE3",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(secondEntry);

        // Assert
        tracker.ConnectedNodes.Should().HaveCount(1);
        tracker.ConnectedNodes[0].Name.Should().Be("NODE3");
    }

    [Fact]
    public void ProcessNodeJoinedLine_ShouldAddNodeAndRaiseEvent()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var nodeJoined = false;
        ConnectedNodeInfo? capturedNode = null;

        tracker.OnNodeJoined += node =>
        {
            nodeJoined = true;
            capturedNode = node;
        };

        var logEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node joined: HB9GXP2-H",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(logEntry);

        // Assert
        nodeJoined.Should().BeTrue();
        capturedNode.Should().NotBeNull();
        capturedNode!.Name.Should().Be("HB9GXP2-H");
        tracker.ConnectedNodes.Should().HaveCount(1);
        tracker.ConnectedNodes[0].Name.Should().Be("HB9GXP2-H");
    }

    [Fact]
    public void ProcessNodeJoinedLine_WithDuplicateNode_ShouldNotAddDuplicate()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var joinEventCount = 0;

        tracker.OnNodeJoined += _ => joinEventCount++;

        var firstJoin = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node joined: HB9GXP-H",
            SvxLinkLogLevel.Info
        );

        var secondJoin = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node joined: HB9GXP-H",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(firstJoin);
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(secondJoin);

        // Assert
        tracker.ConnectedNodes.Should().HaveCount(1);
        joinEventCount.Should().Be(1); // Event triggered only once
    }

    [Fact]
    public void ProcessNodeLeftLine_ShouldRemoveNodeAndRaiseEvent()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var nodeLeft = false;
        ConnectedNodeInfo? capturedNode = null;

        tracker.OnNodeLeft += node =>
        {
            nodeLeft = true;
            capturedNode = node;
        };

        // Ajouter d'abord un nœud
        var joinEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node joined: HB9GXP-H",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(joinEntry);

        var leaveEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node left: HB9GXP-H",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(leaveEntry);

        // Assert
        nodeLeft.Should().BeTrue();
        capturedNode.Should().NotBeNull();
        capturedNode!.Name.Should().Be("HB9GXP-H");
        tracker.ConnectedNodes.Should().BeEmpty();
    }

    [Fact]
    public void ProcessNodeLeftLine_WithNonExistentNode_ShouldNotRaiseEvent()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var leaveEventCount = 0;

        tracker.OnNodeLeft += _ => leaveEventCount++;

        var leaveEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node left: NONEXISTENT",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(leaveEntry);

        // Assert
        tracker.ConnectedNodes.Should().BeEmpty();
        leaveEventCount.Should().Be(0);
    }

    [Fact]
    public void CompleteWorkflow_ShouldHandleMultipleNodesCorrectly()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        // Act & Assert - Initialisation avec 2 nœuds
        var initEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Connected nodes: NODE1, NODE2",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(initEntry);
        tracker.ConnectedNodes.Should().HaveCount(2);

        // Un 3ème nœud rejoint
        var joinEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node joined: NODE3",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(joinEntry);
        tracker.ConnectedNodes.Should().HaveCount(3);

        // NODE1 part
        var leaveEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node left: NODE1",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(leaveEntry);
        tracker.ConnectedNodes.Should().HaveCount(2);
        tracker.ConnectedNodes.Should().Contain(n => n.Name == "NODE2");
        tracker.ConnectedNodes.Should().Contain(n => n.Name == "NODE3");
        tracker.ConnectedNodes.Should().NotContain(n => n.Name == "NODE1");
    }

    [Fact]
    public void UnrelatedLogLines_ShouldBeIgnored()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var eventCount = 0;

        tracker.OnNodeJoined += _ => eventCount++;
        tracker.OnNodeLeft += _ => eventCount++;
        tracker.OnNodesInitialized += _ => eventCount++;

        var unrelatedEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Using audio codec OPUS",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(unrelatedEntry);

        // Assert
        tracker.ConnectedNodes.Should().BeEmpty();
        eventCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromLogService()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        // Act
        tracker.Dispose();

        // Assert
        _logService.Received(1).OnLogReceived -= Arg.Any<Action<SvxLinkLogEntry>>();
    }

    [Fact]
    public void TxStart_ShouldRaiseEventAndMarkNodeAsTx()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        ConnectedNodeInfo? capturedNode = null;

        tracker.OnNodeTxStarted += node => capturedNode = node;

        var joinEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node joined: HB9GXP-H",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(joinEntry);

        var txEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Talker start: HB9GXP-H",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(txEntry);

        // Assert
        capturedNode.Should().NotBeNull();
        capturedNode!.Name.Should().Be("HB9GXP-H");
        capturedNode.IsTx.Should().BeTrue();
        tracker.ConnectedNodes.Single(n => n.Name == "HB9GXP-H").IsTx.Should().BeTrue();
    }

    [Fact]
    public void TxStop_ShouldRaiseEventAndClearTxState()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        ConnectedNodeInfo? capturedNode = null;

        tracker.OnNodeTxStopped += node => capturedNode = node;

        var joinEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node joined: HB9GXP-H",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(joinEntry);

        var txStartEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Talker start: HB9GXP-H",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(txStartEntry);

        var txStopEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Talker stop: HB9GXP-H",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(txStopEntry);

        // Assert
        capturedNode.Should().NotBeNull();
        capturedNode!.Name.Should().Be("HB9GXP-H");
        capturedNode.IsTx.Should().BeFalse();
        tracker.ConnectedNodes.Single(n => n.Name == "HB9GXP-H").IsTx.Should().BeFalse();
    }

    [Fact]
    public void TxStop_WithoutPriorTxStart_ShouldNotRaiseEvent()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var txStopEventCount = 0;

        tracker.OnNodeTxStopped += _ => txStopEventCount++;

        var joinEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node joined: HB9GXP-H",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(joinEntry);

        var txStopEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Talker stop: HB9GXP-H",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(txStopEntry);

        // Assert
        txStopEventCount.Should().Be(0);
        tracker.ConnectedNodes.Single(n => n.Name == "HB9GXP-H").IsTx.Should().BeFalse();
    }

    [Fact]
    public void TxStart_OnAbsentNode_ShouldNotRaiseEvent()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var txStartEventCount = 0;

        tracker.OnNodeTxStarted += _ => txStartEventCount++;

        var txEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Talker start: UNKNOWN-NODE",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(txEntry);

        // Assert
        txStartEventCount.Should().Be(0);
        tracker.ConnectedNodes.Should().BeEmpty();
    }

    [Fact]
    public void Reset_ShouldClearTxState()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        var joinEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node joined: HB9GXP-H",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(joinEntry);

        var txEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Talker start: HB9GXP-H",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(txEntry);
        tracker.ConnectedNodes.Single(n => n.Name == "HB9GXP-H").IsTx.Should().BeTrue();

        // Act
        tracker.Reset();

        // Assert
        tracker.ConnectedNodes.Should().BeEmpty();
    }

    [Fact]
    public void NodeLeft_ShouldClearTxState()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        // Ajouter un nœud puis le mettre en TX
        var joinEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node joined: HB9GXP-H",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(joinEntry);

        var txEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Talker start: HB9GXP-H",
            SvxLinkLogLevel.Info
        );
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(txEntry);
        tracker.ConnectedNodes.Single(n => n.Name == "HB9GXP-H").IsTx.Should().BeTrue();

        var leaveEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Node left: HB9GXP-H",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(leaveEntry);

        // Assert - le nœud est retiré (et donc son état TX aussi)
        tracker.ConnectedNodes.Should().BeEmpty();
    }

    [Fact]
    public async Task ConnectedNodes_ShouldBeThreadSafe()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var tasks = new List<Task>();

        // Act - Simuler plusieurs ajouts concurrents
        for (int i = 0; i < 10; i++)
        {
            var nodeIndex = i;
            tasks.Add(Task.Run(() =>
            {
                var entry = new SvxLinkLogEntry(
                    DateTime.Now,
                    $"ReflectorLogic: Node joined: NODE{nodeIndex}",
                    SvxLinkLogLevel.Info
                );
                _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(entry);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Tous les nœuds doivent être présents sans corruption
        tracker.ConnectedNodes.Should().HaveCount(10);
        tracker.ConnectedNodes.Select(n => n.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Reset_ShouldRaiseOnResetEvent()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var resetEventFired = false;
        tracker.OnReset += () => resetEventFired = true;

        // Act
        tracker.Reset();

        // Assert
        resetEventFired.Should().BeTrue();
    }

    /// <summary>
    /// L'ordre d'invocation de <c>Reset()</c> est délibéré : <c>OnNodesInitialized(vide)</c>
    /// puis <c>OnReset</c>.
    ///
    /// <c>ReflectorConnectionAnnouncementService.OnConnectionReset()</c> écoute <c>OnReset</c>
    /// pour s'armer, et son <c>OnNodesInitialized()</c> joue l'annonce de connexion réussie
    /// dès que le service est armé. Inverser l'ordre armerait le service juste avant de lui
    /// livrer la liste vide du reset : une annonce de connexion serait jouée alors qu'aucune
    /// connexion n'a eu lieu — exactement la régression corrigée par l'issue #83.
    /// </summary>
    [Fact]
    public void Reset_ShouldRaiseOnNodesInitializedBeforeOnReset()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var callOrder = new List<string>();
        tracker.OnReset += () => callOrder.Add("OnReset");
        tracker.OnNodesInitialized += _ => callOrder.Add("OnNodesInitialized");

        // Act
        tracker.Reset();

        // Assert — la liste vide doit être livrée AVANT que les consommateurs ne s'arment
        callOrder.Should().ContainInOrder("OnNodesInitialized", "OnReset");
    }

    #region Talkgroup des nœuds (API de statut du réflecteur)

    /// <summary>
    /// Statut disponible : chaque nœud connu porte son talkgroup, ce qui rend le
    /// regroupement possible sur le tableau de bord.
    /// </summary>
    [Fact]
    public void ConnectedNodes_ShouldCarryTheTalkGroupFromTheReflectorStatus()
    {
        GivenReflectorStatus(("HB9GXP-H", 240), ("HB9GXP3-H", 2403));
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        RaiseLog("ReflectorLogic: Connected nodes: HB9GXP-H, HB9GXP3-H");

        tracker.ConnectedNodes.Should().SatisfyRespectively(
            first => first.TalkGroup.Should().Be(240),
            second => second.TalkGroup.Should().Be(2403));
    }

    /// <summary>
    /// Réflecteur distant : son API de statut n'est pas accessible. Le talkgroup reste nul,
    /// et l'interface conserve alors la liste plate plutôt que de tout ranger sous
    /// « aucun talkgroup », ce qui serait faux.
    /// </summary>
    [Fact]
    public void ConnectedNodes_WithoutReflectorStatus_ShouldLeaveTheTalkGroupUnknown()
    {
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        RaiseLog("ReflectorLogic: Connected nodes: HB9GXP-H, HB9GXP3-H");

        tracker.ConnectedNodes.Should().OnlyContain(node => node.TalkGroup == null);
    }

    [Fact]
    public void ConnectedNodes_ForANodeAbsentFromTheStatus_ShouldLeaveItsTalkGroupUnknown()
    {
        // Les deux sources ne sont pas synchrones : un nœud peut être vu dans les logs
        // avant d'apparaître dans l'instantané, lu toutes les cinq secondes.
        GivenReflectorStatus(("HB9GXP-H", 240));
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        RaiseLog("ReflectorLogic: Connected nodes: HB9GXP-H, HB9GXP3-H");

        tracker.ConnectedNodes.Should().ContainSingle(node => node.Name == "HB9GXP3-H")
            .Which.TalkGroup.Should().BeNull();
    }

    [Fact]
    public void NodeJoined_ShouldCarryItsTalkGroup()
    {
        GivenReflectorStatus(("HB9GXP3-H", 2403));
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        ConnectedNodeInfo? joined = null;
        tracker.OnNodeJoined += node => joined = node;

        RaiseLog("ReflectorLogic: Node joined: HB9GXP3-H");

        joined!.TalkGroup.Should().Be(2403);
    }

    /// <summary>
    /// Un changement de talkgroup ne produit aucune ligne de log : sans cette republication,
    /// le regroupement resterait figé sur la photo prise à la connexion.
    /// </summary>
    [Fact]
    public void AReflectorStatusChange_ShouldRepublishTheNodesWithTheirNewTalkGroups()
    {
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        RaiseLog("ReflectorLogic: Connected nodes: HB9GXP3-H");

        IReadOnlyList<ConnectedNodeInfo>? republished = null;
        tracker.OnNodesInitialized += nodes => republished = nodes;

        RaiseStatusChanged(("HB9GXP3-H", 2409900));

        republished.Should().ContainSingle().Which.TalkGroup.Should().Be(2409900);
    }

    [Fact]
    public void AReflectorStatusChange_ShouldNotAnnounceArrivalsOrDepartures()
    {
        // OnNodeJoined déclenche une notification côté interface : la faire sonner à chaque
        // changement de talkgroup serait insupportable.
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        RaiseLog("ReflectorLogic: Connected nodes: HB9GXP3-H");

        var joined = 0;
        var left = 0;
        tracker.OnNodeJoined += _ => joined++;
        tracker.OnNodeLeft += _ => left++;

        RaiseStatusChanged(("HB9GXP3-H", 2409900));

        joined.Should().Be(0);
        left.Should().Be(0);
    }

    [Fact]
    public void AReflectorStatusChange_WithoutAnyKnownNode_ShouldPublishNothing()
    {
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);
        var published = 0;
        tracker.OnNodesInitialized += _ => published++;

        RaiseStatusChanged(("HB9GXP3-H", 2403));

        published.Should().Be(0);
    }

    [Fact]
    public void TalkGroupLookup_ShouldIgnoreTheCallsignCase()
    {
        GivenReflectorStatus(("hb9gxp3-h", 2403));
        var tracker = new ConnectedNodesTracker(_logger, _logService, _statusService);

        RaiseLog("ReflectorLogic: Connected nodes: HB9GXP3-H");

        tracker.ConnectedNodes.Should().ContainSingle().Which.TalkGroup.Should().Be(2403);
    }

    private void GivenReflectorStatus(params (string Callsign, int TalkGroup)[] nodes) =>
        _statusService.Current.Returns(BuildSnapshot(nodes));

    private void RaiseStatusChanged(params (string Callsign, int TalkGroup)[] nodes)
    {
        var snapshot = BuildSnapshot(nodes);
        _statusService.Current.Returns(snapshot);
        _statusService.OnStatusChanged += Raise.Event<Action<ReflectorStatusSnapshot>>(snapshot);
    }

    private static ReflectorStatusSnapshot BuildSnapshot((string Callsign, int TalkGroup)[] nodes) =>
        new(ReflectorStatusAvailability.Available,
            nodes.Select(n => new ReflectorNodeStatus(
                n.Callsign, n.TalkGroup, [], false, false, null, null, null, null, null)).ToList(),
            DateTime.UtcNow);

    private void RaiseLog(string message) =>
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(
            new SvxLinkLogEntry(DateTime.Now, message, SvxLinkLogLevel.Info));

    #endregion
}
