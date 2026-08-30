using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.Runtime;

namespace SvxlinkManagerV2.Infrastructure.Tests.Runtime;

/// <summary>
/// Tests unitaires de l'implémentation simulée du contrôle de l'alimentation.
/// </summary>
public class SystemControlMockServiceTests
{
    [Fact]
    public void GetAvailability_ShouldReportSimulatedSupport()
    {
        var availability = CreateService().GetAvailability();

        availability.IsSupported.Should().BeTrue();
        availability.IsSimulated.Should().BeTrue();
        availability.UnsupportedReason.Should().BeNull();
    }

    [Fact]
    public async Task RebootAsync_ShouldSucceedWithoutSystemCall()
    {
        var result = await CreateService().RebootAsync();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ShutdownAsync_ShouldSucceedWithoutSystemCall()
    {
        var result = await CreateService().ShutdownAsync();

        result.IsSuccess.Should().BeTrue();
    }

    private static SystemControlMockService CreateService()
        => new(Substitute.For<ILogger<SystemControlMockService>>());
}
