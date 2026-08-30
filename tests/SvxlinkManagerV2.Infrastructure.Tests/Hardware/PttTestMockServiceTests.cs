using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.Hardware;

namespace SvxlinkManagerV2.Infrastructure.Tests.Hardware;

/// <summary>
/// Tests de la machine à états du test d'émission, exercée via son implémentation simulée :
/// durée bornée, relâchement automatique, arrêt manuel et relâchement à l'arrêt de l'application.
/// </summary>
public class PttTestMockServiceTests
{
    [Fact]
    public void State_ShouldStartIdle()
    {
        var state = CreateService().State;

        state.IsTransmitting.Should().BeFalse();
        state.EndsAt.Should().BeNull();
        state.IsSimulated.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ShouldEnterTransmission()
    {
        var service = CreateService();

        var result = await service.StartAsync(5);

        result.ShouldBeSuccess(state =>
        {
            state.IsTransmitting.Should().BeTrue();
            state.EndsAt.Should().NotBeNull();
            state.RemainingSeconds.Should().BeInRange(4, 5);
        });
        service.State.IsTransmitting.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ShouldRaiseStateChanged()
    {
        var service = CreateService();
        PttTestState? observed = null;
        service.OnStateChanged += state => observed = state;

        await service.StartAsync(5);

        observed.Should().NotBeNull();
        observed!.IsTransmitting.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ShouldFail_WhenDurationExceedsTheConfiguredMaximum()
    {
        var service = CreateService(maxDurationSeconds: 10);

        var result = await service.StartAsync(11);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "PTT_TEST_DURATION_TOO_LONG"));
        service.State.IsTransmitting.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task StartAsync_ShouldFail_WhenDurationIsNotPositive(int duration)
    {
        var result = await CreateService().StartAsync(duration);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "PTT_TEST_DURATION_INVALID"));
    }

    [Fact]
    public async Task StartAsync_ShouldFail_WhenATestIsAlreadyRunning()
    {
        var service = CreateService();
        await service.StartAsync(5);

        var result = await service.StartAsync(5);

        result.ShouldBeFail(errors => errors.Should().Contain(error => error.Code == "CONFLICT"));
        service.State.IsTransmitting.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_ShouldReleaseImmediately()
    {
        var service = CreateService();
        await service.StartAsync(30);

        var result = await service.StopAsync();

        result.ShouldBeSuccess(state => state.IsTransmitting.Should().BeFalse());
        service.State.EndsAt.Should().BeNull();
    }

    [Fact]
    public async Task StopAsync_ShouldSucceed_WhenNoTestIsRunning()
    {
        var result = await CreateService().StopAsync();

        result.ShouldBeSuccess(state => state.IsTransmitting.Should().BeFalse());
    }

    [Fact]
    public async Task StartAsync_ShouldReleaseOnItsOwn_WhenTheDurationElapses()
    {
        var service = CreateService();
        var released = new TaskCompletionSource();
        service.OnStateChanged += state =>
        {
            if (!state.IsTransmitting)
                released.TrySetResult();
        };

        await service.StartAsync(1);

        var completed = await Task.WhenAny(released.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        completed.Should().BeSameAs(released.Task, "le PTT doit se relâcher seul à l'échéance");
        service.State.IsTransmitting.Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_ShouldReleaseAnOngoingTest()
    {
        var service = CreateService();
        await service.StartAsync(30);

        service.Dispose();

        service.State.IsTransmitting.Should().BeFalse();
    }

    [Fact]
    public void DefaultDurationSeconds_ShouldBeCappedByTheMaximum()
    {
        var service = CreateService(defaultDurationSeconds: 60, maxDurationSeconds: 10);

        service.DefaultDurationSeconds.Should().Be(10);
    }

    private static PttTestMockService CreateService(
        int defaultDurationSeconds = 5,
        int maxDurationSeconds = 30)
    {
        var options = new AudioOptions
        {
            PttTestDurationSeconds = defaultDurationSeconds,
            PttTestMaxDurationSeconds = maxDurationSeconds
        };

        return new PttTestMockService(
            Options.Create(options),
            Substitute.For<ILogger<PttTestMockService>>());
    }
}
