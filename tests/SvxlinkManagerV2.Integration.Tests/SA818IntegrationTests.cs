using FluentAssertions;
using LanguageExt.UnitTesting;
using Marten;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Features.SA818.GetSA818Configuration;
using SvxlinkManagerV2.Application.Features.SA818.UpdateSA818Configuration;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Infrastructure.Persistence.Projections;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Tests d'intégration validant le workflow complet SA818 :
/// Command → Event Sourcing → Projection → Query
/// </summary>
[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public class SA818IntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private IDocumentSession _session = null!;
    private SA818Repository _repository = null!;

    public SA818IntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Initialise une nouvelle session et nettoie les données AVANT chaque test pour l'isolation
    /// </summary>
    public async Task InitializeAsync()
    {
        // Créer une nouvelle session pour ce test
        _session = _fixture.DocumentStore.LightweightSession();
        _repository = new SA818Repository(_session);

        // Nettoyer toutes les projections SA818 des tests précédents
        _session.DeleteWhere<SA818Projection>(x => true);
        await _session.SaveChangesAsync();
    }

    /// <summary>
    /// Nettoie la session après chaque test
    /// </summary>
    public Task DisposeAsync()
    {
        _session?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task UpdateSA818Configuration_ShouldPersistEventAndUpdateProjection()
    {
        // Arrange
        var command = new UpdateSA818ConfigurationCommand(
            Volume: 5,
            Squelch: 3,
            Bandwidth: SA818Bandwidth.Narrow12_5kHz,
            PreEmph: true,
            HighPass: true,
            LowPass: false
        );

        // Act - Exécuter la commande (création du SA818 car n'existe pas encore)
        var commandResult = await UpdateSA818ConfigurationCommandHandler.Handle(
            command,
            _repository,
            CancellationToken.None
        );

        // Sauvegarder les changements pour déclencher la projection
        await _session.SaveChangesAsync();

        // Assert - Valider que la commande a réussi
        commandResult.ShouldBeSuccess();

        // Valider que la projection a été mise à jour
        var query = new GetSA818ConfigurationQuery();
        var queryResult = await GetSA818ConfigurationQueryHandler.Handle(
            query,
            _repository,
            CancellationToken.None
        );

        queryResult.ShouldBeSuccess(config =>
        {
            config.Id.Should().Be(SA818Aggregate.FixedId);
            config.Volume.Should().Be(5);
            config.Squelch.Should().Be(3);
            config.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
            config.PreEmph.Should().BeTrue();
            config.HighPass.Should().BeTrue();
            config.LowPass.Should().BeFalse();
            config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        });
    }

    [Fact]
    public async Task UpdateSA818Configuration_MultipleTimes_ShouldKeepLatestConfiguration()
    {
        // Arrange - Première configuration
        var firstCommand = new UpdateSA818ConfigurationCommand(
            Volume: 4,
            Squelch: 2,
            Bandwidth: SA818Bandwidth.Wide25kHz,
            PreEmph: false,
            HighPass: false,
            LowPass: true
        );

        await UpdateSA818ConfigurationCommandHandler.Handle(firstCommand, _repository, CancellationToken.None);
        await _session.SaveChangesAsync();

        // Act - Deuxième configuration (mise à jour)
        var secondCommand = new UpdateSA818ConfigurationCommand(
            Volume: 7,
            Squelch: 5,
            Bandwidth: SA818Bandwidth.Narrow12_5kHz,
            PreEmph: true,
            HighPass: true,
            LowPass: true
        );

        var updateResult = await UpdateSA818ConfigurationCommandHandler.Handle(
            secondCommand,
            _repository,
            CancellationToken.None
        );
        await _session.SaveChangesAsync();

        // Assert
        updateResult.ShouldBeSuccess();

        // Vérifier que la projection contient la dernière configuration
        var query = new GetSA818ConfigurationQuery();
        var queryResult = await GetSA818ConfigurationQueryHandler.Handle(
            query,
            _repository,
            CancellationToken.None
        );

        queryResult.ShouldBeSuccess(config =>
        {
            config.Volume.Should().Be(7);
            config.Squelch.Should().Be(5);
            config.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
            config.PreEmph.Should().BeTrue();
            config.HighPass.Should().BeTrue();
            config.LowPass.Should().BeTrue();
        });
    }

    [Fact]
    public async Task GetSA818Configuration_WhenNotInitialized_ShouldReturnFailure()
    {
        // Arrange - Aucune configuration créée

        // Act
        var query = new GetSA818ConfigurationQuery();
        var queryResult = await GetSA818ConfigurationQueryHandler.Handle(
            query,
            _repository,
            CancellationToken.None
        );

        // Assert - Doit retourner une erreur NotFound
        queryResult.ShouldBeFail(errors =>
        {
            errors.Should().ContainSingle();
            errors.Head.Code.Should().Be("SA818_NOT_FOUND");
        });
    }

    [Fact]
    public async Task UpdateSA818Configuration_WithInvalidVolume_ShouldReturnFailure()
    {
        // Arrange - Volume invalide (hors plage 1-8)
        var invalidCommand = new UpdateSA818ConfigurationCommand(
            Volume: 10, // Invalide
            Squelch: 3,
            Bandwidth: SA818Bandwidth.Narrow12_5kHz,
            PreEmph: true,
            HighPass: true,
            LowPass: false
        );

        // Act
        var commandResult = await UpdateSA818ConfigurationCommandHandler.Handle(
            invalidCommand,
            _repository,
            CancellationToken.None
        );

        // Assert - Doit échouer à cause de la validation
        commandResult.ShouldBeFail(errors =>
        {
            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Code.Contains("VOLUME"));
        });
    }
}
