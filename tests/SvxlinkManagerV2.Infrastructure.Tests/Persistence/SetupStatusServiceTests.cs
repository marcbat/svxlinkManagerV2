using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Infrastructure.Persistence;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests unitaires pour SetupStatusService.
/// </summary>
public class SetupStatusServiceTests
{
    private readonly ISalonRepository _salonRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SetupStatusService> _logger;

    public SetupStatusServiceTests()
    {
        _salonRepository = Substitute.For<ISalonRepository>();
        _logger = Substitute.For<ILogger<SetupStatusService>>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISalonRepository)).Returns(_salonRepository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);
    }

    [Fact]
    public async Task IsSetupRequiredAsync_WhenNoSalons_ShouldReturnTrue()
    {
        // Arrange
        _salonRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SalonAggregate>().AsReadOnly());

        var service = new SetupStatusService(_scopeFactory, _logger);

        // Act
        var result = await service.IsSetupRequiredAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSetupRequiredAsync_WhenSalonsExist_ShouldReturnFalse()
    {
        // Arrange
        var salon = CreateSalon();
        _salonRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SalonAggregate> { salon }.AsReadOnly());

        var service = new SetupStatusService(_scopeFactory, _logger);

        // Act
        var result = await service.IsSetupRequiredAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsSetupRequiredAsync_WhenCalledTwice_ShouldUseCacheOnSecondCall()
    {
        // Arrange
        _salonRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SalonAggregate>().AsReadOnly());

        var service = new SetupStatusService(_scopeFactory, _logger);

        // Act
        await service.IsSetupRequiredAsync();
        await service.IsSetupRequiredAsync();

        // Assert — repository appelé une seule fois grâce au cache
        await _salonRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsSetupRequiredAsync_AfterInvalidateCache_ShouldQueryRepositoryAgain()
    {
        // Arrange
        _salonRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SalonAggregate>().AsReadOnly());

        var service = new SetupStatusService(_scopeFactory, _logger);

        // Act
        await service.IsSetupRequiredAsync();
        service.InvalidateCache();
        await service.IsSetupRequiredAsync();

        // Assert — repository appelé deux fois après invalidation du cache
        await _salonRepository.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsSetupRequiredAsync_AfterInvalidateCache_ShouldReturnUpdatedValue()
    {
        // Arrange — d'abord aucun salon, puis un salon après invalidation
        _salonRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(
                new List<SalonAggregate>().AsReadOnly(),
                new List<SalonAggregate> { CreateSalon() }.AsReadOnly());

        var service = new SetupStatusService(_scopeFactory, _logger);

        // Act
        var firstResult = await service.IsSetupRequiredAsync();
        service.InvalidateCache();
        var secondResult = await service.IsSetupRequiredAsync();

        // Assert
        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse();
    }

    [Fact]
    public void InvalidateCache_WhenCacheIsEmpty_ShouldNotThrow()
    {
        // Arrange
        var service = new SetupStatusService(_scopeFactory, _logger);

        // Act + Assert — ne doit pas lever d'exception
        var act = () => service.InvalidateCache();
        act.Should().NotThrow();
    }

    private static SalonAggregate CreateSalon()
    {
        var result = SalonAggregate.Create(
            id: Guid.NewGuid(),
            name: "Salon Test",
            isDefault: false,
            isTemporized: false,
            configuration: new Domain.Aggregates.Salon.Entities.SvxLinkConfiguration(
                Id: Guid.NewGuid(),
                Logics: "SimplexLogic,ReflectorLogic",
                CfgDir: "svxlink.d",
                CardSampleRate: 16000,
                CardChannels: 1,
                Host: "test.example.com",
                Port: 5300,
                Callsign: "F5ABC",
                AuthKey: "TestKey",
                JitterBufferDelay: 0,
                ReflectorProtocol: Domain.Aggregates.Salon.Enums.ReflectorProtocol.V2,
                CertEmail: null,
                SimplexCallsign: "F5ABC-L",
                Modules: "ModuleHelp",
                ShortIdentInterval: 600,
                LongIdentInterval: 3600,
                ReportCtcss: null,
                DefaultLang: "fr_FR",
                RgrSoundDelay: 0,
                RxFrequency: 145.500m,
                TxFrequency: 145.500m,
                RxCtcss: null,
                TxCtcss: null));

        return result.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException("Failed to create test salon"));
    }
}
