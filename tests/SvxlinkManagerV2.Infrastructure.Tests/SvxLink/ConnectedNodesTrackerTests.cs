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

    public ConnectedNodesTrackerTests()
    {
        _logger = Substitute.For<ILogger<ConnectedNodesTracker>>();
        _logService = Substitute.For<ISvxLinkLogService>();
    }

    [Fact]
    public void Constructor_ShouldSubscribeToLogService()
    {
        // Arrange & Act
        var tracker = new ConnectedNodesTracker(_logger, _logService);

        // Assert
        _logService.Received(1).OnLogReceived += Arg.Any<Action<SvxLinkLogEntry>>();
    }

    [Fact]
    public void ConnectedNodes_Initially_ShouldBeEmpty()
    {
        // Arrange & Act
        var tracker = new ConnectedNodesTracker(_logger, _logService);

        // Assert
        tracker.ConnectedNodes.Should().BeEmpty();
    }

    [Fact]
    public void ProcessConnectedNodesLine_ShouldInitializeNodesList()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService);
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
        var tracker = new ConnectedNodesTracker(_logger, _logService);
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
        var tracker = new ConnectedNodesTracker(_logger, _logService);

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
        var tracker = new ConnectedNodesTracker(_logger, _logService);
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
        var tracker = new ConnectedNodesTracker(_logger, _logService);
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
        var tracker = new ConnectedNodesTracker(_logger, _logService);
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
        var tracker = new ConnectedNodesTracker(_logger, _logService);
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
        var tracker = new ConnectedNodesTracker(_logger, _logService);

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
        var tracker = new ConnectedNodesTracker(_logger, _logService);
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
        var tracker = new ConnectedNodesTracker(_logger, _logService);

        // Act
        tracker.Dispose();

        // Assert
        _logService.Received(1).OnLogReceived -= Arg.Any<Action<SvxLinkLogEntry>>();
    }

    [Fact]
    public async Task ConnectedNodes_ShouldBeThreadSafe()
    {
        // Arrange
        var tracker = new ConnectedNodesTracker(_logger, _logService);
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
}
