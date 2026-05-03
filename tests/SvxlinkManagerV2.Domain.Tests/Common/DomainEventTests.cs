using FluentAssertions;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Tests.Common;

/// <summary>
/// Tests unitaires pour la classe abstraite DomainEvent
/// </summary>
public class DomainEventTests
{
    // Événement de test concret
    private record TestDomainEvent(string Data) : DomainEvent;

    [Fact]
    public void DomainEvent_Should_Have_OccurredOn_Set_To_UtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var @event = new TestDomainEvent("test");
        var afterCreation = DateTime.UtcNow;

        // Assert
        @event.OccurredOn.Should().BeOnOrAfter(beforeCreation);
        @event.OccurredOn.Should().BeOnOrBefore(afterCreation);
        @event.OccurredOn.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void DomainEvent_Should_Have_Unique_EventId()
    {
        // Arrange & Act
        var event1 = new TestDomainEvent("test1");
        var event2 = new TestDomainEvent("test2");

        // Assert
        event1.EventId.Should().NotBe(Guid.Empty);
        event2.EventId.Should().NotBe(Guid.Empty);
        event1.EventId.Should().NotBe(event2.EventId);
    }

    [Fact]
    public void DomainEvent_Should_Be_Record_And_Support_With_Expression()
    {
        // Arrange
        var originalEvent = new TestDomainEvent("original");

        // Act
        var modifiedEvent = originalEvent with { Data = "modified" };

        // Assert
        modifiedEvent.Data.Should().Be("modified");
        modifiedEvent.EventId.Should().Be(originalEvent.EventId);
        modifiedEvent.OccurredOn.Should().Be(originalEvent.OccurredOn);
    }

    [Fact]
    public void DomainEvents_With_Same_Values_Should_Be_Equal()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var occurredOn = DateTime.UtcNow;
        var event1 = new TestDomainEvent("data") { EventId = eventId, OccurredOn = occurredOn };
        var event2 = new TestDomainEvent("data") { EventId = eventId, OccurredOn = occurredOn };

        // Act & Assert
        event1.Should().Be(event2);
    }

    [Fact]
    public void DomainEvents_With_Different_Data_Should_Not_Be_Equal()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var occurredOn = DateTime.UtcNow;
        var event1 = new TestDomainEvent("data1") { EventId = eventId, OccurredOn = occurredOn };
        var event2 = new TestDomainEvent("data2") { EventId = eventId, OccurredOn = occurredOn };

        // Act & Assert
        event1.Should().NotBe(event2);
    }

    [Fact]
    public void Multiple_Events_Created_Sequentially_Should_Have_Chronological_OccurredOn()
    {
        // Arrange & Act
        var event1 = new TestDomainEvent("first");
        Thread.Sleep(10); // Petit délai pour garantir une différence de timestamp
        var event2 = new TestDomainEvent("second");
        Thread.Sleep(10);
        var event3 = new TestDomainEvent("third");

        // Assert
        event2.OccurredOn.Should().BeOnOrAfter(event1.OccurredOn);
        event3.OccurredOn.Should().BeOnOrAfter(event2.OccurredOn);
    }

    [Fact]
    public void DomainEvent_Properties_Should_Be_Immutable_Via_Init()
    {
        // Arrange
        var @event = new TestDomainEvent("test");
        var newEventId = Guid.NewGuid();
        var newOccurredOn = DateTime.UtcNow.AddDays(-1);

        // Act
        var modifiedEvent = @event with 
        { 
            EventId = newEventId, 
            OccurredOn = newOccurredOn 
        };

        // Assert
        modifiedEvent.EventId.Should().Be(newEventId);
        modifiedEvent.OccurredOn.Should().Be(newOccurredOn);
        @event.EventId.Should().NotBe(newEventId);
        @event.OccurredOn.Should().NotBe(newOccurredOn);
    }
}
