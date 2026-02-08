using FluentAssertions;
using LanguageExt.UnitTesting;
using Marten;
using SvxlinkManagerV2.Application.Features.SA818.GetSA818Configuration;
using SvxlinkManagerV2.Application.Features.SA818.UpdateSA818Configuration;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.SA818.Events;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Features;

/// <summary>
/// Tests d'intégration pour les Commands et Queries SA818.
/// Valide le workflow complet : Command → Événement → Projection → Query.
/// </summary>
[Trait("Category", "Integration")]
public class SA818IntegrationTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private IDocumentSession _session = null!;
    private SA818Repository _repository = null!;

    public SA818IntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Créer une nouvelle session pour ce test
        _session = _fixture.DocumentStore.LightweightSession();
        _repository = new SA818Repository(_session);

        // Nettoyer toutes les données des tests précédents
        await _fixture.DocumentStore.Advanced.Clean.CompletelyRemoveAllAsync();
    }

    public Task DisposeAsync()
    {
        _session?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task UpdateCommand_ShouldCreateSA818_WhenNotExists()
    {
        // Arrange
        var command = new UpdateSA818ConfigurationCommand(
            Volume: 5,
            Squelch: 3,
            Bandwidth: SA818Bandwidth.Wide25kHz,
            PreEmph: true,
            HighPass: false,
            LowPass: true);

        // Act - Exécuter la commande
        var commandResult = await UpdateSA818ConfigurationCommandHandler.Handle(
            command,
            _repository,
            CancellationToken.None);

        // Assert - Vérifier que la commande a réussi
        commandResult.ShouldBeSuccess();

        // Vérifier que l'événement a été persisté dans le stream
        var events = await _session.Events.FetchStreamAsync(SA818Aggregate.FixedId);
        events.Should().NotBeEmpty();
        events.Should().HaveCount(1);
        events.First().EventType.Should().Be(typeof(SA818ConfigurationUpdatedEvent));
    }

    [Fact]
    public async Task UpdateCommandAndGetQuery_ShouldReturnCorrectConfiguration()
    {
        // Arrange
        var command = new UpdateSA818ConfigurationCommand(
            Volume: 6,
            Squelch: 4,
            Bandwidth: SA818Bandwidth.Narrow12_5kHz,
            PreEmph: false,
            HighPass: true,
            LowPass: false);

        // Act - Exécuter la commande
        var commandResult = await UpdateSA818ConfigurationCommandHandler.Handle(
            command,
            _repository,
            CancellationToken.None);

        commandResult.ShouldBeSuccess();

        // Attendre que la projection soit mise à jour (inline projection)
        await _session.SaveChangesAsync();

        // Exécuter la query
        var query = new GetSA818ConfigurationQuery();
        var queryResult = await GetSA818ConfigurationQueryHandler.Handle(
            query,
            _repository,
            CancellationToken.None);

        // Assert - Vérifier que la query retourne les bonnes données
        queryResult.ShouldBeSuccess(config =>
        {
            config.Id.Should().Be(SA818Aggregate.FixedId);
            config.Volume.Should().Be(6);
            config.Squelch.Should().Be(4);
            config.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
            config.PreEmph.Should().BeFalse();
            config.HighPass.Should().BeTrue();
            config.LowPass.Should().BeFalse();
            config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        });
    }

    [Fact]
    public async Task UpdateCommand_MultipleTimes_ShouldAppendEventsAndUpdateProjection()
    {
        // Arrange - Première configuration
        var command1 = new UpdateSA818ConfigurationCommand(
            Volume: 3,
            Squelch: 2,
            Bandwidth: SA818Bandwidth.Wide25kHz,
            PreEmph: false,
            HighPass: false,
            LowPass: false);

        // Act - Première mise à jour
        var result1 = await UpdateSA818ConfigurationCommandHandler.Handle(
            command1,
            _repository,
            CancellationToken.None);

        result1.ShouldBeSuccess();
        await _session.SaveChangesAsync();

        // Arrange - Deuxième configuration (différente)
        var command2 = new UpdateSA818ConfigurationCommand(
            Volume: 7,
            Squelch: 5,
            Bandwidth: SA818Bandwidth.Narrow12_5kHz,
            PreEmph: true,
            HighPass: true,
            LowPass: true);

        // Act - Deuxième mise à jour
        var result2 = await UpdateSA818ConfigurationCommandHandler.Handle(
            command2,
            _repository,
            CancellationToken.None);

        result2.ShouldBeSuccess();
        await _session.SaveChangesAsync();

        // Assert - Vérifier que 2 événements ont été ajoutés
        var events = await _session.Events.FetchStreamAsync(SA818Aggregate.FixedId);
        events.Should().HaveCount(2);

        // Assert - Vérifier que la projection reflète la dernière configuration
        var query = new GetSA818ConfigurationQuery();
        var queryResult = await GetSA818ConfigurationQueryHandler.Handle(
            query,
            _repository,
            CancellationToken.None);

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
    public async Task GetQuery_WhenSA818NotInitialized_ShouldReturnNotFoundError()
    {
        // Arrange - Ne créer AUCUN SA818

        // Act
        var query = new GetSA818ConfigurationQuery();
        var queryResult = await GetSA818ConfigurationQueryHandler.Handle(
            query,
            _repository,
            CancellationToken.None);

        // Assert - Vérifier que la query retourne une erreur NotFound
        queryResult.ShouldBeFail(errors =>
        {
            errors.Should().NotBeEmpty();
            errors.Head().Code.Should().Contain("NOT_FOUND");
        });
    }

    [Fact]
    public async Task UpdateCommand_WithInvalidVolume_ShouldReturnValidationError()
    {
        // Arrange - Volume invalide (hors plage 1-8)
        var command = new UpdateSA818ConfigurationCommand(
            Volume: 10, // Valeur invalide
            Squelch: 4,
            Bandwidth: SA818Bandwidth.Wide25kHz,
            PreEmph: false,
            HighPass: false,
            LowPass: false);

        // Act
        var result = await UpdateSA818ConfigurationCommandHandler.Handle(
            command,
            _repository,
            CancellationToken.None);

        // Assert - Vérifier que la commande échoue avec une erreur de validation
        result.ShouldBeFail(errors =>
        {
            errors.Should().NotBeEmpty();
            var error = errors.Head();
            error.Code.Should().Be("SA818_VOLUME_INVALID");
            error.Message.Should().Contain("volume");
        });

        // Vérifier qu'aucun événement n'a été persisté
        var events = await _session.Events.FetchStreamAsync(SA818Aggregate.FixedId);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateCommand_WithInvalidSquelch_ShouldReturnValidationError()
    {
        // Arrange - Squelch invalide (hors plage 0-8)
        var command = new UpdateSA818ConfigurationCommand(
            Volume: 4,
            Squelch: 15, // Valeur invalide
            Bandwidth: SA818Bandwidth.Wide25kHz,
            PreEmph: false,
            HighPass: false,
            LowPass: false);

        // Act
        var result = await UpdateSA818ConfigurationCommandHandler.Handle(
            command,
            _repository,
            CancellationToken.None);

        // Assert - Vérifier que la commande échoue avec une erreur de validation
        result.ShouldBeFail(errors =>
        {
            errors.Should().NotBeEmpty();
            var error = errors.Head();
            error.Code.Should().Be("SA818_SQUELCH_INVALID");
            error.Message.Should().Contain("squelch");
        });
    }

    [Fact]
    public async Task CompleteWorkflow_CreateUpdateQuery_ShouldWorkEndToEnd()
    {
        // Arrange - Configuration initiale
        var createCommand = new UpdateSA818ConfigurationCommand(
            Volume: 4,
            Squelch: 4,
            Bandwidth: SA818Bandwidth.Wide25kHz,
            PreEmph: false,
            HighPass: false,
            LowPass: false);

        // Act 1 - Créer le SA818
        var createResult = await UpdateSA818ConfigurationCommandHandler.Handle(
            createCommand,
            _repository,
            CancellationToken.None);

        createResult.ShouldBeSuccess();
        await _session.SaveChangesAsync();

        // Act 2 - Lire la configuration
        var getQuery = new GetSA818ConfigurationQuery();
        var getResult = await GetSA818ConfigurationQueryHandler.Handle(
            getQuery,
            _repository,
            CancellationToken.None);

        getResult.ShouldBeSuccess(config =>
        {
            config.Volume.Should().Be(4);
            config.Squelch.Should().Be(4);
        });

        // Act 3 - Mettre à jour la configuration
        var updateCommand = new UpdateSA818ConfigurationCommand(
            Volume: 8,
            Squelch: 8,
            Bandwidth: SA818Bandwidth.Narrow12_5kHz,
            PreEmph: true,
            HighPass: true,
            LowPass: true);

        var updateResult = await UpdateSA818ConfigurationCommandHandler.Handle(
            updateCommand,
            _repository,
            CancellationToken.None);

        updateResult.ShouldBeSuccess();
        await _session.SaveChangesAsync();

        // Act 4 - Relire la configuration mise à jour
        var getUpdatedResult = await GetSA818ConfigurationQueryHandler.Handle(
            getQuery,
            _repository,
            CancellationToken.None);

        // Assert - Vérifier que la configuration a bien été mise à jour
        getUpdatedResult.ShouldBeSuccess(config =>
        {
            config.Volume.Should().Be(8);
            config.Squelch.Should().Be(8);
            config.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
            config.PreEmph.Should().BeTrue();
            config.HighPass.Should().BeTrue();
            config.LowPass.Should().BeTrue();
        });

        // Vérifier que 2 événements sont dans le stream
        var events = await _session.Events.FetchStreamAsync(SA818Aggregate.FixedId);
        events.Should().HaveCount(2);
    }
}
