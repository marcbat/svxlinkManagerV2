using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Features.SA818.UpdateSA818Configuration;
using SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;
using SvxlinkManagerV2.Application.Features.Salons.CreateSalon;
using SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Tests d'intégration validant le workflow complet d'activation d'un Salon.
/// </summary>
[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public class SalonActivationIntegrationTests : IAsyncLifetime
{
    private readonly SqliteFixture _fixture;
    private SvxlinkDbContext _context = null!;
    private SalonRepository _salonRepository = null!;
    private SA818Repository _sa818Repository = null!;

    private ISA818Service _sa818ServiceMock = null!;
    private ISvxLinkConfigurationService _configServiceMock = null!;
    private ISvxLinkDaemonService _daemonServiceMock = null!;
    private IActiveSessionTracker _trackerMock = null!;
    private IConnectedNodesService _connectedNodesMock = null!;
    private ILogger<ActivateSalonCommandHandler> _loggerMock = null!;

    public SalonActivationIntegrationTests(SqliteFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _salonRepository = new SalonRepository(_context);
        _sa818Repository = new SA818Repository(_context);

        _sa818ServiceMock = Substitute.For<ISA818Service>();
        _configServiceMock = Substitute.For<ISvxLinkConfigurationService>();
        _daemonServiceMock = Substitute.For<ISvxLinkDaemonService>();
        _trackerMock = Substitute.For<IActiveSessionTracker>();
        _connectedNodesMock = Substitute.For<IConnectedNodesService>();
        _loggerMock = Substitute.For<ILogger<ActivateSalonCommandHandler>>();

        _sa818ServiceMock.ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>())
            .Returns(Success<global::LanguageExt.Common.Error, global::LanguageExt.Unit>(unit));
        _configServiceMock.GenerateAsync(Arg.Any<Domain.Aggregates.Salon.SalonAggregate>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Success<global::LanguageExt.Common.Error, global::LanguageExt.Unit>(unit));
        _daemonServiceMock.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Success<global::LanguageExt.Common.Error, global::LanguageExt.Unit>(unit));
        _daemonServiceMock.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Success<global::LanguageExt.Common.Error, global::LanguageExt.Unit>(unit));
        _trackerMock.ActiveSalonId.Returns((Guid?)null);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ActivateSalon_ShouldExecuteCompleteWorkflowWithAllSideEffects()
    {
        // Arrange
        var sa818Handler = new UpdateSA818ConfigurationCommandHandler(_sa818Repository);
        await sa818Handler.Handle(new UpdateSA818ConfigurationCommand(5, 3, SA818Bandwidth.Narrow12_5kHz, true, true, false), CancellationToken.None);

        var salonId = Guid.NewGuid();
        var createHandler = new CreateSalonCommandHandler(_salonRepository);
        await createHandler.Handle(new CreateSalonCommand(
            salonId, "Salon National France", true, false,
            145.550m, 145.575m, 136.5m, 136.5m,
            CreateValidConfiguration()), CancellationToken.None);

        // Act
        var activateHandler = new ActivateSalonCommandHandler(
            _salonRepository, _trackerMock, _sa818Repository,
            _sa818ServiceMock, _configServiceMock, _daemonServiceMock,
            _connectedNodesMock, _loggerMock);

        var activateResult = await activateHandler.Handle(new ActivateSalonCommand(salonId), CancellationToken.None);

        // Assert
        activateResult.ShouldBeSuccess();

        await _configServiceMock.Received(1).GenerateAsync(
            Arg.Is<Domain.Aggregates.Salon.SalonAggregate>(s => s.Id == salonId),
            Arg.Is<string>(path => path.Contains("svxlink.conf")),
            Arg.Any<CancellationToken>());

        _trackerMock.Received(1).SetActiveSalon(salonId);
    }

    [Fact]
    public async Task ActivateSalon_WithNullCtcss_ShouldUseCode0000()
    {
        // Arrange
        var sa818Handler = new UpdateSA818ConfigurationCommandHandler(_sa818Repository);
        await sa818Handler.Handle(new UpdateSA818ConfigurationCommand(6, 4, SA818Bandwidth.Narrow12_5kHz, true, true, false), CancellationToken.None);

        var salonId = Guid.NewGuid();
        var createHandler = new CreateSalonCommandHandler(_salonRepository);
        await createHandler.Handle(new CreateSalonCommand(
            salonId, "Salon Sans CTCSS", false, false,
            145.550m, 145.550m, null, null,
            CreateValidConfiguration()), CancellationToken.None);

        // Act
        var activateHandler = new ActivateSalonCommandHandler(
            _salonRepository, _trackerMock, _sa818Repository,
            _sa818ServiceMock, _configServiceMock, _daemonServiceMock,
            _connectedNodesMock, _loggerMock);

        await activateHandler.Handle(new ActivateSalonCommand(salonId), CancellationToken.None);

        // Assert - Code CTCSS "0000" pour pas de CTCSS
        await _sa818ServiceMock.Received(1).ConfigureAsync(
            Arg.Is<SA818CommandSet>(cmd => cmd.DmoSetGroup.Contains("0000")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateSalon_WhenSA818NotConfigured_ShouldFail()
    {
        // Arrange - Créer un Salon SANS configurer le SA818
        var salonId = Guid.NewGuid();
        var createHandler = new CreateSalonCommandHandler(_salonRepository);
        await createHandler.Handle(new CreateSalonCommand(
            salonId, "Salon Test", false, false,
            145.550m, 145.550m, null, null,
            CreateValidConfiguration()), CancellationToken.None);

        // Act
        var activateHandler = new ActivateSalonCommandHandler(
            _salonRepository, _trackerMock, _sa818Repository,
            _sa818ServiceMock, _configServiceMock, _daemonServiceMock,
            _connectedNodesMock, _loggerMock);

        var result = await activateHandler.Handle(new ActivateSalonCommand(salonId), CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SA818_CONFIG_NOT_FOUND");
        });
        _trackerMock.DidNotReceive().SetActiveSalon(Arg.Any<Guid>());
    }

    private static SvxLinkConfiguration CreateValidConfiguration()
    {
        return new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d", 16000, 1,
            "ref.f5kri.fr", 5300,
            "F5ABC-L", "test-auth-key-123", "OPUS", 0,
            "F5ABC", "ModuleHelp,ModuleParrot", 60, 60,
            "71.9", "/usr/share/svxlink/events.tcl", "fr_FR", 0,
            Guid.NewGuid(), 145.550m, 145.550m, 136.5m, 136.5m);
    }
}
