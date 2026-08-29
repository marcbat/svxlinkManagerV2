using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.Runtime;

namespace SvxlinkManagerV2.Infrastructure.Tests.Runtime;

/// <summary>
/// Tests unitaires du service de contrôle de l'alimentation.
/// Seules les configurations qui rendent l'action indisponible sont testées : aucun appel système
/// ne doit être déclenché depuis la suite de tests, quelle que soit la plateforme d'exécution.
/// </summary>
public class SystemControlServiceTests
{
    [Fact]
    public void GetAvailability_ShouldReportUnsupported_WhenDisabledByConfiguration()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["SystemControl:Enabled"] = "false"
        });

        var availability = service.GetAvailability();

        availability.IsSupported.Should().BeFalse();
        availability.IsSimulated.Should().BeFalse();
        availability.UnsupportedReason.Should().Contain("SystemControl:Enabled");
    }

    [Fact]
    public void GetAvailability_ShouldReportUnsupported_WhenCommandBinaryIsMissing()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["SystemControl:Enabled"] = "true",
            ["SystemControl:RebootCommand"] = "/chemin/inexistant/systemctl reboot",
            ["SystemControl:ShutdownCommand"] = "/chemin/inexistant/systemctl poweroff"
        });

        var availability = service.GetAvailability();

        availability.IsSupported.Should().BeFalse();
        availability.UnsupportedReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RebootAsync_ShouldFail_WhenActionsAreDisabled()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["SystemControl:Enabled"] = "false"
        });

        var result = await service.RebootAsync();

        result.IsFail.Should().BeTrue();
        result.Match(
            Succ: _ => Assert.Fail("Un échec était attendu"),
            Fail: errors => errors.Head.Code.Should().Be("SYSTEM_CONTROL_UNSUPPORTED"));
    }

    [Fact]
    public async Task ShutdownAsync_ShouldFail_WhenActionsAreDisabled()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["SystemControl:Enabled"] = "false"
        });

        var result = await service.ShutdownAsync();

        result.IsFail.Should().BeTrue();
        result.Match(
            Succ: _ => Assert.Fail("Un échec était attendu"),
            Fail: errors => errors.Head.Code.Should().Be("SYSTEM_CONTROL_UNSUPPORTED"));
    }

    [Fact]
    public async Task RebootAsync_ShouldFail_WhenCommandBinaryIsMissing()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["SystemControl:Enabled"] = "true",
            ["SystemControl:RebootCommand"] = "/chemin/inexistant/systemctl reboot",
            ["SystemControl:ShutdownCommand"] = "/chemin/inexistant/systemctl poweroff"
        });

        var result = await service.RebootAsync();

        result.IsFail.Should().BeTrue();
    }

    private static SystemControlService CreateService(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new SystemControlService(
            configuration,
            Substitute.For<ILogger<SystemControlService>>());
    }
}
