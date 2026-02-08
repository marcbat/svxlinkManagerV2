using FluentAssertions;
using LanguageExt.UnitTesting;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Features.SA818.UpdateSA818Configuration;
using SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;
using SvxlinkManagerV2.Application.Features.Salons.CreateSalon;
using SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;
using SvxlinkManagerV2.Application.Features.Salons.GetSalonById;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Events;
using SvxlinkManagerV2.Infrastructure.Persistence.Projections;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// **TEST CRITIQUE** : Valide le workflow complet d'activation d'un Salon avec side-effect.
/// 
/// Workflow testé :
/// 1. Création de la configuration SA818 (hardware)
/// 2. Création d'un Salon (config radio)
/// 3. Activation du Salon → SalonActivated event
/// 4. Side-effect handler (SalonActivatedHandler) exécuté :
///    - Fusion paramètres Salon + SA818 → commandes AT
///    - Configuration hardware via ISA818Service
///    - Génération svxlink.conf via ISvxLinkConfigurationService
///    - Redémarrage daemon via ISvxLinkDaemonService
/// 5. Projection mise à jour : IsActive = true
/// </summary>
[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public class SalonActivationIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private IDocumentSession _session = null!;
    private SalonRepository _salonRepository = null!;
    private SA818Repository _sa818Repository = null!;
    
    // Mocks des services hardware
    private ISA818Service _sa818ServiceMock = null!;
    private ISvxLinkConfigurationService _configServiceMock = null!;
    private ISvxLinkDaemonService _daemonServiceMock = null!;

    public SalonActivationIntegrationTests(PostgresFixture fixture)
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
        _salonRepository = new SalonRepository(_session);
        _sa818Repository = new SA818Repository(_session);

        // Nettoyer toutes les projections des tests précédents
        _session.DeleteWhere<SalonProjection>(x => true);
        _session.DeleteWhere<SA818Projection>(x => true);
        await _session.SaveChangesAsync();

        // Créer les mocks pour les services hardware
        _sa818ServiceMock = Substitute.For<ISA818Service>();
        _configServiceMock = Substitute.For<ISvxLinkConfigurationService>();
        _daemonServiceMock = Substitute.For<ISvxLinkDaemonService>();

        // Configurer les mocks pour retourner des succès par défaut
        _sa818ServiceMock.ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>())
            .Returns(Success<LanguageExt.Common.Error, LanguageExt.Unit>(unit));
        
        _configServiceMock.GenerateAsync(Arg.Any<Domain.Aggregates.Salon.SalonAggregate>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Success<LanguageExt.Common.Error, LanguageExt.Unit>(unit));
        
        _daemonServiceMock.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Success<LanguageExt.Common.Error, LanguageExt.Unit>(unit));
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
    public async Task ActivateSalon_ShouldExecuteCompleteWorkflowWithAllSideEffects()
    {
        // ============================================================
        // ARRANGE - Setup complet : SA818 + Salon
        // ============================================================

        // Étape 1 : Créer la configuration SA818 (hardware)
        var sa818Command = new UpdateSA818ConfigurationCommand(
            Volume: 5,
            Squelch: 3,
            Bandwidth: SA818Bandwidth.Narrow12_5kHz,
            PreEmph: true,
            HighPass: true,
            LowPass: false
        );

        await UpdateSA818ConfigurationCommandHandler.Handle(sa818Command, _sa818Repository, CancellationToken.None);
        await _session.SaveChangesAsync();

        // Étape 2 : Créer un Salon (config radio)
        var salonId = Guid.NewGuid();
        var configuration = CreateValidConfiguration();
        var createSalonCommand = new CreateSalonCommand(
            Id: salonId,
            Name: "Salon National France",
            IsDefault: true,
            IsTemporized: false,
            RxFrequency: 145.550m,
            TxFrequency: 145.575m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m,
            Configuration: configuration
        );

        await CreateSalonCommandHandler.Handle(createSalonCommand, _salonRepository, CancellationToken.None);
        await _session.SaveChangesAsync();

        // ============================================================
        // ACT - Activer le Salon
        // ============================================================

        // Étape 3 : Activer le Salon (génère l'événement SalonActivated)
        var activateCommand = new ActivateSalonCommand(salonId);
        var activateResult = await ActivateSalonCommandHandler.Handle(
            activateCommand,
            _salonRepository,
            CancellationToken.None
        );
        await _session.SaveChangesAsync();

        // Valider que l'activation a réussi
        activateResult.ShouldBeSuccess();

        // Étape 4 : Récupérer l'événement SalonActivated et exécuter le side-effect manuellement
        // (Dans un vrai environnement Wolverine, ceci serait automatique)
        var salon = await _salonRepository.GetByIdAsync(salonId, CancellationToken.None);
        var salonAggregate = salon.Match(
            Succ: s => s,
            Fail: _ => throw new InvalidOperationException("Salon not found")
        );

        // Créer l'événement manuellement (dans la vraie app, Wolverine le fait automatiquement)
        var salonActivatedEvent = new SalonActivated(salonId);

        // Exécuter le side-effect handler
        var sideEffectResult = await SalonActivatedHandler.Handle(
            salonActivatedEvent,
            _salonRepository,
            _sa818Repository,
            _sa818ServiceMock,
            _configServiceMock,
            _daemonServiceMock,
            NullLogger.Instance,
            CancellationToken.None
        );

        await _session.SaveChangesAsync();

        // ============================================================
        // ASSERT - Vérifications complètes
        // ============================================================

        // Vérification 1 : Side-effect handler a réussi
        sideEffectResult.ShouldBeSuccess();

        // Vérification 2 : Le mock ISA818Service.ConfigureAsync() a été appelé
        await _sa818ServiceMock.Received(1).ConfigureAsync(
            Arg.Is<SA818CommandSet>(cmd =>
                cmd.DmoSetGroup.Contains(",145,5750,145,5500,0021,") && // Freq format avec virgules
                cmd.DmoSetVolume.Contains("5") &&       // Volume
                cmd.SetFilter.Contains("1")),           // PreEmph activé
            Arg.Any<CancellationToken>()
        );

        // Vérification 3 : Le mock ISvxLinkConfigurationService.GenerateAsync() a été appelé
        await _configServiceMock.Received(1).GenerateAsync(
            Arg.Is<Domain.Aggregates.Salon.SalonAggregate>(s => s.Id == salonId),
            Arg.Is<string>(path => path.Contains("svxlink.conf")),
            Arg.Any<CancellationToken>()
        );

        // Vérification 4 : Le mock ISvxLinkDaemonService.RestartAsync() a été appelé
        await _daemonServiceMock.Received(1).RestartAsync(Arg.Any<CancellationToken>());

        // Vérification 5 : La projection du Salon indique IsActive = true
        var activeSalonQuery = new GetActiveSalonQuery();
        var activeSalon = await GetActiveSalonQueryHandler.Handle(
            activeSalonQuery,
            _salonRepository,
            CancellationToken.None
        );

        activeSalon.Should().NotBeNull();
        activeSalon!.Id.Should().Be(salonId);
        activeSalon.IsActive.Should().BeTrue();
        activeSalon.Name.Should().Be("Salon National France");

        // Vérification 6 : GetSalonById retourne le Salon actif
        var getSalonQuery = new GetSalonByIdQuery(salonId);
        var getSalonResult = await GetSalonByIdQueryHandler.Handle(
            getSalonQuery,
            _salonRepository,
            CancellationToken.None
        );

        getSalonResult.ShouldBeSuccess(s =>
        {
            s.IsActive.Should().BeTrue();
            s.Configuration.RxFrequency.Should().Be(145.550m);
            s.Configuration.TxFrequency.Should().Be(145.575m);
        });
    }

    [Fact]
    public async Task ActivateSalon_ShouldMapCtcssFrequencyToSA818Codes()
    {
        // ============================================================
        // ARRANGE - Créer SA818 + Salon avec CTCSS spécifiques
        // ============================================================

        // SA818
        var sa818Command = new UpdateSA818ConfigurationCommand(
            Volume: 4,
            Squelch: 2,
            Bandwidth: SA818Bandwidth.Wide25kHz,
            PreEmph: false,
            HighPass: false,
            LowPass: true
        );
        await UpdateSA818ConfigurationCommandHandler.Handle(sa818Command, _sa818Repository, CancellationToken.None);
        await _session.SaveChangesAsync();

        // Salon avec CTCSS 123.0 Hz (code SA818 : 0015)
        var salonId = Guid.NewGuid();
        var configuration = CreateValidConfiguration();
        var createCommand = new CreateSalonCommand(
            salonId,
            "Salon CTCSS Test",
            false,
            false,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 123.0m, // CTCSS 123.0 Hz → code SA818 = 0015
            TxCtcss: 123.0m,
            Configuration: configuration
        );
        await CreateSalonCommandHandler.Handle(createCommand, _salonRepository, CancellationToken.None);
        await _session.SaveChangesAsync();

        // ============================================================
        // ACT - Activer le Salon et exécuter side-effect
        // ============================================================

        var activateCommand = new ActivateSalonCommand(salonId);
        await ActivateSalonCommandHandler.Handle(activateCommand, _salonRepository, CancellationToken.None);
        await _session.SaveChangesAsync();

        var salonActivatedEvent = new SalonActivated(salonId);
        await SalonActivatedHandler.Handle(
            salonActivatedEvent,
            _salonRepository,
            _sa818Repository,
            _sa818ServiceMock,
            _configServiceMock,
            _daemonServiceMock,
            NullLogger.Instance,
            CancellationToken.None
        );

        // ============================================================
        // ASSERT - Vérifier conversion CTCSS
        // ============================================================

        // Vérifier que le code CTCSS SA818 "0018" (123.0 Hz) est utilisé
        await _sa818ServiceMock.Received(1).ConfigureAsync(
            Arg.Is<SA818CommandSet>(cmd =>
                cmd.DmoSetGroup.Contains("0018")), // Code SA818 pour 123.0 Hz
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ActivateSalon_WithNullCtcss_ShouldUseCode0000()
    {
        // ============================================================
        // ARRANGE - Salon SANS CTCSS
        // ============================================================

        // SA818
        var sa818Command = new UpdateSA818ConfigurationCommand(6, 4, SA818Bandwidth.Narrow12_5kHz, true, true, false);
        await UpdateSA818ConfigurationCommandHandler.Handle(sa818Command, _sa818Repository, CancellationToken.None);
        await _session.SaveChangesAsync();

        // Salon sans CTCSS
        var salonId = Guid.NewGuid();
        var configuration = CreateValidConfiguration();
        var createCommand = new CreateSalonCommand(
            salonId,
            "Salon Sans CTCSS",
            false,
            false,
            145.550m,
            145.550m,
            RxCtcss: null, // Pas de CTCSS
            TxCtcss: null,
            configuration
        );
        await CreateSalonCommandHandler.Handle(createCommand, _salonRepository, CancellationToken.None);
        await _session.SaveChangesAsync();

        // ============================================================
        // ACT
        // ============================================================

        var activateCommand = new ActivateSalonCommand(salonId);
        await ActivateSalonCommandHandler.Handle(activateCommand, _salonRepository, CancellationToken.None);
        await _session.SaveChangesAsync();

        var salonActivatedEvent = new SalonActivated(salonId);
        await SalonActivatedHandler.Handle(
            salonActivatedEvent,
            _salonRepository,
            _sa818Repository,
            _sa818ServiceMock,
            _configServiceMock,
            _daemonServiceMock,
            NullLogger.Instance,
            CancellationToken.None
        );

        // ============================================================
        // ASSERT - Code CTCSS doit être "0000" (aucun CTCSS)
        // ============================================================

        await _sa818ServiceMock.Received(1).ConfigureAsync(
            Arg.Is<SA818CommandSet>(cmd =>
                cmd.DmoSetGroup.Contains("0000")), // Code SA818 pour "pas de CTCSS"
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ActivateSalon_WhenSA818NotConfigured_ShouldFail()
    {
        // ============================================================
        // ARRANGE - Créer un Salon SANS configurer le SA818 d'abord
        // ============================================================

        var salonId = Guid.NewGuid();
        var configuration = CreateValidConfiguration();
        var createCommand = new CreateSalonCommand(
            salonId,
            "Salon Test",
            false,
            false,
            145.550m,
            145.550m,
            null,
            null,
            configuration
        );
        await CreateSalonCommandHandler.Handle(createCommand, _salonRepository, CancellationToken.None);
        await _session.SaveChangesAsync();

        // ============================================================
        // ACT - Activer le Salon (doit échouer car SA818 non configuré)
        // ============================================================

        var activateCommand = new ActivateSalonCommand(salonId);
        await ActivateSalonCommandHandler.Handle(activateCommand, _salonRepository, CancellationToken.None);
        await _session.SaveChangesAsync();

        var salonActivatedEvent = new SalonActivated(salonId);
        var sideEffectResult = await SalonActivatedHandler.Handle(
            salonActivatedEvent,
            _salonRepository,
            _sa818Repository,
            _sa818ServiceMock,
            _configServiceMock,
            _daemonServiceMock,
            NullLogger.Instance,
            CancellationToken.None
        );

        // ============================================================
        // ASSERT - Le side-effect doit échouer
        // ============================================================

        sideEffectResult.ShouldBeFail(errors =>
        {
            errors.Should().ContainSingle();
            errors.Head.Message.Should().Contain("SA818");
        });
    }

    #region Helper Methods

    private static SvxLinkConfiguration CreateValidConfiguration()
    {
        return new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d",
            16000,
            1,
            "ref.f5kri.fr",
            5300,
            "F5ABC-L",
            "test-auth-key-123",
            "OPUS",
            0,
            "F5ABC",
            "ModuleHelp,ModuleParrot",
            60,
            60,
            "71.9",
            "/usr/share/svxlink/events.tcl",
            "fr_FR",
            0,
            Guid.NewGuid(),
            145.550m,
            145.550m,
            136.5m,
            136.5m);
    }

    #endregion
}
