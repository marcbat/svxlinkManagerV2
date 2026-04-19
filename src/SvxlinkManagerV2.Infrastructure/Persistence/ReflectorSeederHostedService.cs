using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Service de seeding automatique du réflecteur local au premier démarrage de l'application.
/// Idempotent : si des réflecteurs existent déjà, le seeding est ignoré.
/// Contrairement au SalonSeederHostedService, ce seeder s'exécute même si le wizard de
/// configuration est requis : la config du réflecteur est indépendante du callsign utilisateur.
/// La configuration INI générée est compatible SVXLink 25.05 (protocole V3, certificats X.509).
/// </summary>
public class ReflectorSeederHostedService : IHostedService
{
    /// <summary>
    /// GUID fixe du réflecteur local par défaut (cohérence entre installations).
    /// </summary>
    internal static readonly Guid DefaultReflectorId = new("b2d4e6f8-1a3c-5e7d-9f0b-2c4d6e8f0a1b");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReflectorSeederHostedService> _logger;
    private readonly IHostEnvironment _environment;

    public ReflectorSeederHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReflectorSeederHostedService> logger,
        IHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Exécuté au démarrage de l'application.
    /// Sème le réflecteur local par défaut si la base est vide (idempotent).
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ReflectorSeederHostedService: Vérification existence des réflecteurs...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reflectorRepository = scope.ServiceProvider.GetRequiredService<IReflectorRepository>();

            var existingReflectors = await reflectorRepository.GetAllAsync(cancellationToken);

            if (existingReflectors.Count > 0)
            {
                _logger.LogInformation(
                    "Réflecteurs déjà existants ({Count}), initialisation ignorée.",
                    existingReflectors.Count);
                return;
            }

            if (_environment.IsProduction())
            {
                _logger.LogWarning(
                    "ReflectorSeederHostedService: ATTENTION — aucun réflecteur trouvé en environnement Production. " +
                    "Démarrage du seeding du réflecteur local par défaut.");
            }
            else
            {
                _logger.LogInformation("Aucun réflecteur trouvé, seeding du réflecteur local par défaut...");
            }

            var createResult = ReflectorAggregate.Create(
                id: DefaultReflectorId,
                name: "Réflecteur Local",
                config: GetDefaultReflectorConfig());

            await createResult.Match(
                async aggregate =>
                {
                    var saveResult = await reflectorRepository.SaveAsync(aggregate, cancellationToken);

                    saveResult.Match(
                        _ =>
                        {
                            _logger.LogInformation(
                                "Réflecteur seedé avec succès : Réflecteur Local (ID: {Id})",
                                DefaultReflectorId);
                        },
                        errors =>
                        {
                            _logger.LogError(
                                "Erreur lors de la sauvegarde du réflecteur 'Réflecteur Local': {Errors}",
                                string.Join(", ", errors.Select(e => e.Message)));
                        });
                },
                errors =>
                {
                    _logger.LogError(
                        "Erreur lors de la création du réflecteur 'Réflecteur Local': {Errors}",
                        string.Join(", ", errors.Select(e => e.Message)));
                    return Task.CompletedTask;
                });

            _logger.LogInformation("Seeding du réflecteur local terminé.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur critique lors du seeding du réflecteur — seeding interrompu");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retourne la configuration INI par défaut du réflecteur local.
    /// Compatible SVXLink 25.05 — protocole V3 avec certificats X.509.
    /// </summary>
    internal static string GetDefaultReflectorConfig()
    {
        return """
            [GLOBAL]
            TIMESTAMP_FORMAT="%c"
            LISTEN_PORT=5300
            ACCEPT_CALLSIGN=.*
            CODECS=OPUS
            CERT_PKI_DIR=/var/lib/svxlink/pki
            TG_FOR_V1_CLIENTS=0

            [ROOT_CA]
            COMMON_NAME=SvxReflector Root CA
            COUNTRY=CH

            [ISSUING_CA]
            COMMON_NAME=SvxReflector Issuing CA
            COUNTRY=CH

            [SERVER_CERT]
            COMMON_NAME=svxreflector
            SUBJECT_ALT_NAME=DNS:localhost,IP:127.0.0.1

            [TG#0]
            AUTO_QSY_AFTER=0
            ALLOW=.*
            SHOW_ACTIVITY=1

            [USERS]
            # Clients V2 (AUTH_KEY) — Exemple : CALLSIGN=GroupeMotDePasse
            # F5ZZZ-L=Noeuds

            [PASSWORDS]
            # Mots de passe V2 — Exemple : GroupeMotDePasse="MotDePasse"
            # Noeuds="Passw0rd"
            """;
    }
}
