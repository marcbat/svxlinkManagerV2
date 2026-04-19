using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration pour SalonSeederHostedService avec SQLite.
/// </summary>
[Collection("PostgresIntegration")]
public class SalonSeederHostedServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private SvxlinkDbContext _context = null!;
    private ISalonRepository _repository = null!;
    private ILogger<SalonSeederHostedService> _logger = null!;
    private IServiceScopeFactory _scopeFactory = null!;
    private IHostEnvironment _environment = null!;
    private ISetupStatusService _setupStatusService = null!;

    public SalonSeederHostedServiceIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repository = new SalonRepository(_context);
        _logger = Substitute.For<ILogger<SalonSeederHostedService>>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISalonRepository)).Returns(_repository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        _environment = Substitute.For<IHostEnvironment>();
        _environment.EnvironmentName.Returns(Environments.Development);

        // Par défaut, le setup n'est PAS requis → le seeder procède normalement
        _setupStatusService = Substitute.For<ISetupStatusService>();
        _setupStatusService.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(false);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task StartAsync_WhenNoSalonsExist_ShouldCreate7Salons()
    {
        // Arrange
        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var salons = await _repository.GetAllAsync();
        salons.Should().HaveCount(7);
    }

    [Fact]
    public async Task StartAsync_WhenNoSalonsExist_ShouldCreateSalonsWithFixedGuids()
    {
        // Arrange
        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);
        var expectedGuids = new[]
        {
            new Guid("235a4521-15a1-4e02-a540-91ee600452ac"),
            new Guid("1f2e87b8-d984-4c05-8a4a-ffad65c829a9"),
            new Guid("0f669a03-dcf1-4277-9b07-54f6a0fd3037"),
            new Guid("a749ffe5-16c7-45da-809d-c048908f115c"),
            new Guid("d4c59d86-947c-4b1d-831a-807c1877d426"),
            new Guid("9f99b18b-96ea-453d-b07a-7923c09c939f"),
            new Guid("c7a3e2d1-4b8f-4e6a-9d2c-1f5b7e8a3c04"),
        };

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var salons = await _repository.GetAllAsync();
        salons.Select(s => s.Id).Should().BeEquivalentTo(expectedGuids);
    }

    [Fact]
    public async Task StartAsync_WhenNoSalonsExist_AllSalonsShouldHaveIsDefaultFalse()
    {
        // Arrange
        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var salons = await _repository.GetAllAsync();
        salons.Should().AllSatisfy(s => s.IsDefault.Should().BeFalse());
    }

    [Fact]
    public async Task StartAsync_WhenNoSalonsExist_AllSalonsShouldHaveIsTemporizedFalse()
    {
        // Arrange
        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var salons = await _repository.GetAllAsync();
        salons.Should().AllSatisfy(s => s.IsTemporized.Should().BeFalse());
    }

    [Fact]
    public async Task StartAsync_WhenSalonsAlreadyExist_ShouldBeIdempotent()
    {
        // Arrange - Créer un salon existant manuellement
        var existingSalonResult = SalonAggregate.Create(
            id: Guid.NewGuid(),
            name: "Salon Existant",
            isDefault: false,
            isTemporized: false,
            configuration: new SvxLinkConfiguration(
                Id: Guid.NewGuid(),
                Logics: "SimplexLogic,ReflectorLogic",
                CfgDir: "svxlink.d",
                CardSampleRate: 16000,
                CardChannels: 1,
                Host: "test.example.com",
                Port: 5300,
                Callsign: "NOCALL",
                AuthKey: "TestKey123",
                JitterBufferDelay: 0,
                ReflectorProtocol: ReflectorProtocol.V2,
                CertEmail: null,
                SimplexCallsign: "F0ABC",
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

        await existingSalonResult.Match(
            async aggregate => await _repository.SaveAsync(aggregate),
            errors => throw new Exception("Échec création salon initial"));

        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);

        // Act - Exécuter le seeder alors qu'un salon existe déjà
        await service.StartAsync(CancellationToken.None);

        // Assert - Seulement 1 salon (le seeding a été ignoré)
        var salons = await _repository.GetAllAsync();
        salons.Should().HaveCount(1);
        salons[0].Name.Should().Be("Salon Existant");
    }

    [Fact]
    public async Task StartAsync_WhenCalledTwiceOnEmptyDatabase_ShouldCreate7SalonsOnFirstCallOnly()
    {
        // Arrange
        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);

        // Act - Premier démarrage
        await service.StartAsync(CancellationToken.None);

        // Act - Second démarrage (même service, base non-vide maintenant)
        await service.StartAsync(CancellationToken.None);

        // Assert - Toujours 7 salons (pas de doublons)
        var salons = await _repository.GetAllAsync();
        salons.Should().HaveCount(7);
    }

    [Fact]
    public async Task StartAsync_WhenNoSalonsExist_ShouldContainRRFSalon()
    {
        // Arrange
        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Vérifier la présence du salon RRF avec son GUID fixe
        var rrfId = new Guid("235a4521-15a1-4e02-a540-91ee600452ac");
        var salons = await _repository.GetAllAsync();
        var rrf = salons.FirstOrDefault(s => s.Id == rrfId);
        rrf.Should().NotBeNull();
        rrf!.Name.Should().Be("Réseau des Répéteurs Francophones");
        rrf.Configuration.Host.Should().Be("rrf2.f5nlg.ovh");
        rrf.Configuration.Port.Should().Be(5300);
    }

    [Fact]
    public async Task StartAsync_WhenNoSalonsExist_ShouldNotContainObsoleteSalons()
    {
        // Arrange
        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Salon International et Salon Expérimental supprimés du seed
        var salons = await _repository.GetAllAsync();
        salons.Should().NotContain(s => s.Name == "Salon International");
        salons.Should().NotContain(s => s.Name == "Salon Expérimental");
    }

    [Fact]
    public async Task StartAsync_WhenNoSalonsExist_ShouldContainReflecteurLocalSalon()
    {
        // Arrange
        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Vérifier la présence du salon Réflecteur Local (V3, localhost)
        var reflecteurLocalId = new Guid("c7a3e2d1-4b8f-4e6a-9d2c-1f5b7e8a3c04");
        var salons = await _repository.GetAllAsync();
        var reflecteurLocal = salons.FirstOrDefault(s => s.Id == reflecteurLocalId);
        reflecteurLocal.Should().NotBeNull();
        reflecteurLocal!.Name.Should().Be("Réflecteur Local");
        reflecteurLocal.Configuration.Host.Should().Be("127.0.0.1");
        reflecteurLocal.Configuration.Port.Should().Be(5300);
        reflecteurLocal.Configuration.ReflectorProtocol.Should().Be(ReflectorProtocol.V3);
        reflecteurLocal.Configuration.AuthKey.Should().BeNull();
        reflecteurLocal.DtmfCode.Should().Be(210);
    }

    [Fact]
    public async Task StartAsync_WhenNoSalonsExist_AllSalonsShouldHaveDtmfCodeAssigned()
    {
        // Arrange
        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Chaque salon a le bon code DTMF
        var salons = await _repository.GetAllAsync();
        var bySalon = salons.ToDictionary(s => s.Name);

        bySalon["Réseau des Répéteurs Francophones"].DtmfCode.Should().Be(96);
        bySalon["Salon Suisse Romand"].DtmfCode.Should().Be(200);
        bySalon["French Open Network"].DtmfCode.Should().Be(97);
        bySalon["Salon Technique"].DtmfCode.Should().Be(98);
        bySalon["Salon Bavardage"].DtmfCode.Should().Be(100);
        bySalon["Salon Local"].DtmfCode.Should().Be(101);
        bySalon["Réflecteur Local"].DtmfCode.Should().Be(210);
    }

    /// <summary>
    /// Régression : si la lecture de la DB lève une exception (schéma incompatible, DB verrouillée…),
    /// le seeder ne doit PAS écrire de données — il interrompt silencieusement sans détruire les données existantes.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenGetAllAsyncThrowsException_ShouldNotSeedAnySalon()
    {
        // Arrange
        var failingRepository = Substitute.For<ISalonRepository>();
        failingRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<SalonAggregate>>(
                new InvalidOperationException("Erreur SQLite simulée — schéma incompatible")));

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISalonRepository)).Returns(failingRepository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var service = new SalonSeederHostedService(scopeFactory, _logger, _environment, _setupStatusService);

        // Act — ne doit pas lever d'exception (le catch interne absorbe)
        var act = async () => await service.StartAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Assert — aucun salon ne doit avoir été enregistré dans la base
        _ = failingRepository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Régression : un redémarrage avec des données existantes ne doit pas écraser ces données.
    /// Scénario typique post-update .deb : la base contient des salons personnalisés.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenSalonsAlreadyExistInProduction_ShouldNotOverwriteThem()
    {
        // Arrange — simuler un environnement Production avec des salons personnalisés
        var productionEnvironment = Substitute.For<IHostEnvironment>();
        productionEnvironment.EnvironmentName.Returns(Environments.Production);

        var existingSalonResult = SalonAggregate.Create(
            id: Guid.NewGuid(),
            name: "Mon Salon Personnalisé",
            isDefault: true,
            isTemporized: false,
            configuration: new SvxLinkConfiguration(
                Id: Guid.NewGuid(),
                Logics: "SimplexLogic,ReflectorLogic",
                CfgDir: "svxlink.d",
                CardSampleRate: 16000,
                CardChannels: 1,
                Host: "mon-reflecteur.local",
                Port: 5300,
                Callsign: "F0XYZ",
                AuthKey: "MaClePersonnalisee",
                JitterBufferDelay: 0,
                ReflectorProtocol: ReflectorProtocol.V2,
                CertEmail: null,
                SimplexCallsign: "F0XYZ",
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

        await existingSalonResult.Match(
            async aggregate => await _repository.SaveAsync(aggregate),
            errors => throw new Exception("Échec création salon initial"));

        var service = new SalonSeederHostedService(_scopeFactory, _logger, productionEnvironment, _setupStatusService);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — le salon personnalisé est toujours le seul, il n'a pas été remplacé par les salons par défaut
        var salons = await _repository.GetAllAsync();
        salons.Should().HaveCount(1);
        salons[0].Name.Should().Be("Mon Salon Personnalisé");
        salons[0].IsDefault.Should().BeTrue();
    }

    /// <summary>
    /// Nouveau comportement : si la base est vide ET que le wizard de setup est requis,
    /// le seeder doit ignorer le seeding (ce sera fait par CompleteSetupCommand).
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenSetupIsRequired_ShouldNotSeedAnySalon()
    {
        // Arrange — simuler le wizard requis (base vide)
        _setupStatusService.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);
        var service = new SalonSeederHostedService(_scopeFactory, _logger, _environment, _setupStatusService);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — aucun salon seedé
        var salons = await _repository.GetAllAsync();
        salons.Should().BeEmpty();
    }
}
