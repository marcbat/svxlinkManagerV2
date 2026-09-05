using FluentAssertions;
using SvxlinkManagerV2.Domain.Statistics;
using Xunit;

namespace SvxlinkManagerV2.Domain.Tests.Statistics;

/// <summary>
/// Tests de <see cref="SalonSession"/>.
/// </summary>
public class SalonSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 30, 12, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Start_ShouldNormaliseToUtc()
    {
        var session = SalonSession.Start(Guid.NewGuid(), "TG208", SalonKind.Reflector, SalonActivationOrigin.Web, Start);

        session.StartedAt.Offset.Should().Be(TimeSpan.Zero);
        session.StartedAt.Should().Be(Start.ToUniversalTime());
    }

    [Fact]
    public void Start_ShouldBeOpenWithoutDuration()
    {
        var session = SalonSession.Start(null, "Mode autonome", SalonKind.Standalone, SalonActivationOrigin.Startup, Start);

        session.IsOpen.Should().BeTrue();
        session.EndedAt.Should().BeNull();
        session.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Start_ShouldFallBackWhenNameIsBlank()
    {
        var session = SalonSession.Start(Guid.NewGuid(), "   ", SalonKind.Reflector, SalonActivationOrigin.Web, Start);

        session.SalonName.Should().Be("Sans nom");
    }

    [Fact]
    public void Close_ShouldComputeDuration()
    {
        var session = SalonSession.Start(Guid.NewGuid(), "TG208", SalonKind.Reflector, SalonActivationOrigin.Dtmf, Start);

        session.Close(Start.AddMinutes(90));

        session.IsOpen.Should().BeFalse();
        session.Duration.Should().Be(TimeSpan.FromMinutes(90));
        session.ClosedOnRecovery.Should().BeFalse();
    }

    [Fact]
    public void Close_ShouldFlagRecovery()
    {
        var session = SalonSession.Start(Guid.NewGuid(), "TG208", SalonKind.Reflector, SalonActivationOrigin.Web, Start);

        session.Close(Start.AddHours(1), closedOnRecovery: true);

        session.ClosedOnRecovery.Should().BeTrue();
    }

    [Fact]
    public void Close_ShouldNeverProduceNegativeDuration()
    {
        // Horloge reculée, ou clôture sur un événement antérieur à la session lors d'une reprise.
        var session = SalonSession.Start(Guid.NewGuid(), "TG208", SalonKind.Reflector, SalonActivationOrigin.Web, Start);

        session.Close(Start.AddHours(-3));

        session.Duration.Should().Be(TimeSpan.Zero);
        session.EndedAt.Should().Be(session.StartedAt);
    }

    [Fact]
    public void Close_ShouldBeIgnoredOnAnAlreadyClosedSession()
    {
        var session = SalonSession.Start(Guid.NewGuid(), "TG208", SalonKind.Reflector, SalonActivationOrigin.Web, Start);
        session.Close(Start.AddMinutes(10));

        session.Close(Start.AddMinutes(200));

        session.Duration.Should().Be(TimeSpan.FromMinutes(10));
    }
}
