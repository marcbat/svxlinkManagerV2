using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration pour ReflectorSeederHostedService avec SQLite.
/// </summary>
[Collection("PostgresIntegration")]
public class ReflectorSeederHostedServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private SvxlinkDbContext _context = null!;
    private IReflectorRepository _repository = null!;
    private ILogger<ReflectorSeederHostedService> _logger = null!;
    private IServiceScopeFactory _scopeFactory = null!;
    private IHostEnvironment _environment = null!;

    public ReflectorSeederHostedServiceIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repository = new ReflectorRepository(_context);
        _logger = Substitute.For<ILogger<ReflectorSeederHostedService>>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IReflectorRepository)).Returns(_repository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        _environment = Substitute.For<IHostEnvironment>();
        _environment.EnvironmentName.Returns(Environments.Development);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task StartAsync_WhenNoReflectorsExist_ShouldCreateOneReflector()
    {
        // Arrange
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var reflectors = await _repository.GetAllAsync();
        reflectors.Should().HaveCount(1);
    }

    [Fact]
    public async Task StartAsync_WhenNoReflectorsExist_ShouldCreateReflectorWithFixedGuid()
    {
        // Arrange
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var reflectors = await _repository.GetAllAsync();
        reflectors[0].Id.Should().Be(ReflectorSeederHostedService.DefaultReflectorId);
    }

    [Fact]
    public async Task StartAsync_WhenNoReflectorsExist_ShouldCreateReflecteurLocal()
    {
        // Arrange
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var reflectors = await _repository.GetAllAsync();
        var reflector = reflectors[0];
        reflector.Name.Should().Be("Réflecteur Local");
        reflector.Config.Should().Contain("[GLOBAL]");
        reflector.Config.Should().Contain("LISTEN_PORT=5300");
        reflector.Config.Should().Contain("CODECS=OPUS");
        reflector.Config.Should().Contain("CERT_PKI_DIR=/var/lib/svxlink/pki");
    }

    [Fact]
    public async Task StartAsync_WhenNoReflectorsExist_ConfigShouldContainPkiSections()
    {
        // Arrange
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — Les sections PKI sont nécessaires pour le protocole V3 (X.509)
        var reflectors = await _repository.GetAllAsync();
        var config = reflectors[0].Config;
        config.Should().Contain("[ROOT_CA]");
        config.Should().Contain("[ISSUING_CA]");
        config.Should().Contain("[SERVER_CERT]");
        config.Should().Contain("[TG#0]");
    }

    /// <summary>
    /// Sans TG_FOR_V1_CLIENTS, un nœud en protocole V2 ne peut participer à aucun talkgroup
    /// et reste muet dès qu'un talkgroup est utilisé — ce qui arrive dès qu'un nœud V3 est
    /// présent. Le talkgroup désigné doit exister, sans quoi la variable ne pointe sur rien.
    /// </summary>
    [Fact]
    public async Task StartAsync_ConfigShouldLetLegacyNodesJoinATalkGroup()
    {
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        await service.StartAsync(CancellationToken.None);

        var config = (await _repository.GetAllAsync())[0].Config;
        config.Should().Contain($"TG_FOR_V1_CLIENTS={ReflectorRecommendedSettings.LegacyClientsTalkGroup}");
        config.Should().Contain($"[TG#{ReflectorRecommendedSettings.LegacyClientsTalkGroup}]");
    }

    /// <summary>
    /// Sans RANDOM_QSY_RANGE, le QSY aléatoire — et donc AUTO_QSY_AFTER — ne fonctionne pas.
    /// </summary>
    [Fact]
    public async Task StartAsync_ConfigShouldMakeRandomQsyWork()
    {
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        await service.StartAsync(CancellationToken.None);

        var config = (await _repository.GetAllAsync())[0].Config;
        config.Should().Contain($"RANDOM_QSY_RANGE={ReflectorRecommendedSettings.RandomQsyRange}");
    }

    /// <summary>
    /// Le modèle et le diagnostic proposé aux installations existantes doivent dire la même
    /// chose : un réflecteur fraîchement seedé ne doit rien avoir à compléter.
    /// </summary>
    [Fact]
    public async Task StartAsync_ConfigShouldDeclareEveryRecommendedSetting()
    {
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        await service.StartAsync(CancellationToken.None);

        var config = (await _repository.GetAllAsync())[0].Config;
        ReflectorRecommendedSettings.MissingFrom(config).Should().BeEmpty();
    }

    /// <summary>
    /// Le démon refuse de démarrer sur la moindre erreur de syntaxe, en boucle : livrer un
    /// modèle qu'il rejetterait rendrait le réflecteur inutilisable dès l'installation.
    /// </summary>
    [Fact]
    public async Task StartAsync_ConfigShouldBeSyntacticallyValid()
    {
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        await service.StartAsync(CancellationToken.None);

        var config = (await _repository.GetAllAsync())[0].Config;
        ReflectorConfigurationValidator.Validate(config).Should().BeEmpty();
    }

    /// <summary>
    /// Le délimiteur fermant d'une chaîne brute C# décide de l'indentation retirée : mal
    /// aligné, il préfixe chaque ligne d'espaces que le démon pourrait refuser.
    /// </summary>
    [Fact]
    public async Task StartAsync_ConfigShouldNotBeIndented()
    {
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        await service.StartAsync(CancellationToken.None);

        var config = (await _repository.GetAllAsync())[0].Config;
        config.Split('\n')
            .Where(line => line.Trim().Length > 0)
            .Should().OnlyContain(line => !line.StartsWith(" ") && !line.StartsWith("\t"));
    }

    [Fact]
    public async Task StartAsync_WhenReflectorsAlreadyExist_ShouldBeIdempotent()
    {
        // Arrange — créer un réflecteur existant
        var existingResult = ReflectorAggregate.Create(
            id: Guid.NewGuid(),
            name: "Mon Réflecteur Personnalisé",
            config: "[GLOBAL]\nLISTEN_PORT=5301\nCODECS=OPUS");

        await existingResult.Match(
            async aggregate => await _repository.SaveAsync(aggregate),
            errors => throw new Exception("Échec création réflecteur initial"));

        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — le réflecteur personnalisé est toujours le seul
        var reflectors = await _repository.GetAllAsync();
        reflectors.Should().HaveCount(1);
        reflectors[0].Name.Should().Be("Mon Réflecteur Personnalisé");
    }

    [Fact]
    public async Task StartAsync_WhenCalledTwiceOnEmptyDatabase_ShouldCreate1ReflectorOnFirstCallOnly()
    {
        // Arrange
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        // Act
        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        // Assert
        var reflectors = await _repository.GetAllAsync();
        reflectors.Should().HaveCount(1);
    }

    [Fact]
    public async Task StartAsync_WhenSetupIsRequired_ShouldStillSeedReflector()
    {
        // Arrange — la config du réflecteur est indépendante du callsign utilisateur,
        // donc le seeding doit s'exécuter même si le wizard n'est pas encore complété.
        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, _environment);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var reflectors = await _repository.GetAllAsync();
        reflectors.Should().HaveCount(1);
    }

    [Fact]
    public async Task StartAsync_WhenGetAllAsyncThrowsException_ShouldNotSeedAnyReflector()
    {
        // Arrange
        var failingRepository = Substitute.For<IReflectorRepository>();
        failingRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<ReflectorAggregate>>(
                new InvalidOperationException("Erreur SQLite simulée")));

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IReflectorRepository)).Returns(failingRepository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var service = new ReflectorSeederHostedService(scopeFactory, _logger, _environment);

        // Act
        var act = async () => await service.StartAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Assert
        _ = failingRepository.DidNotReceive().SaveAsync(Arg.Any<ReflectorAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenReflectorsAlreadyExistInProduction_ShouldNotOverwriteThem()
    {
        // Arrange
        var productionEnvironment = Substitute.For<IHostEnvironment>();
        productionEnvironment.EnvironmentName.Returns(Environments.Production);

        var existingResult = ReflectorAggregate.Create(
            id: Guid.NewGuid(),
            name: "Réflecteur Production",
            config: "[GLOBAL]\nLISTEN_PORT=5300\nCODECS=OPUS");

        await existingResult.Match(
            async aggregate => await _repository.SaveAsync(aggregate),
            errors => throw new Exception("Échec création réflecteur initial"));

        var service = new ReflectorSeederHostedService(_scopeFactory, _logger, productionEnvironment);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var reflectors = await _repository.GetAllAsync();
        reflectors.Should().HaveCount(1);
        reflectors[0].Name.Should().Be("Réflecteur Production");
    }
}
