using FluentAssertions;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Tests.Common;

/// <summary>
/// Tests unitaires pour la classe AggregateRoot
/// </summary>
public class AggregateRootTests
{
    // Aggregate de test concret
    private class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate() : base() { }
        public TestAggregate(Guid id) : base(id) { }

        public void DoSomething()
        {
            var @event = new TestEvent(Id);
            AddDomainEvent(@event);
        }
    }

    // Événement de test
    private record TestEvent(Guid AggregateId) : DomainEvent;

    [Fact]
    public void AggregateRoot_Should_Have_Id()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var aggregate = new TestAggregate(id);

        // Assert
        aggregate.Id.Should().Be(id);
    }

    [Fact]
    public void DomainEvents_Should_Be_Empty_Initially()
    {
        // Arrange & Act
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Assert
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDomainEvent_Should_Add_Event_To_Collection()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Act
        aggregate.DoSomething();

        // Assert
        aggregate.DomainEvents.Should().HaveCount(1);
        aggregate.DomainEvents.First().Should().BeOfType<TestEvent>();
    }

    [Fact]
    public void AddDomainEvent_Should_Add_Multiple_Events()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Act
        aggregate.DoSomething();
        aggregate.DoSomething();
        aggregate.DoSomething();

        // Assert
        aggregate.DomainEvents.Should().HaveCount(3);
    }

    [Fact]
    public void ClearDomainEvents_Should_Remove_All_Events()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.DoSomething();
        aggregate.DoSomething();

        // Act
        aggregate.ClearDomainEvents();

        // Assert
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_Should_Be_ReadOnly()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.DoSomething();

        // Act
        var events = aggregate.DomainEvents;

        // Assert
        events.Should().BeAssignableTo<IReadOnlyCollection<DomainEvent>>();
    }

    [Fact]
    public void DefaultConstructor_Should_Initialize_Aggregate()
    {
        // Arrange & Act
        var aggregate = new TestAggregate();

        // Assert
        aggregate.Should().NotBeNull();
        aggregate.DomainEvents.Should().BeEmpty();
    }
}
