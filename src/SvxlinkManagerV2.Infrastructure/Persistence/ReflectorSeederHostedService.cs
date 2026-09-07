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
    /// <remarks>
    /// Publique parce que la page Réflecteur s'en sert pour créer le réflecteur quand la
    /// base n'en contient aucun : elle en tenait sa propre copie, et une clé ajoutée ici
    /// manquait alors sur tout réflecteur créé depuis l'interface.
    /// </remarks>
    public static string GetDefaultReflectorConfig()
    {
        return """
            [GLOBAL]
            TIMESTAMP_FORMAT="%c"
            LISTEN_PORT=5300
            ACCEPT_CALLSIGN=.*
            CODECS=OPUS
            CERT_PKI_DIR=/var/lib/svxlink/pki

            # Serveur HTTP de statut, lu par la page Réflecteur pour lister les nœuds
            # connectés et leur talkgroup.
            #
            # SVXLink lie ce port sur toutes les interfaces (Async::TcpServer sans adresse,
            # non configurable) : sur une machine exposée, il doit être fermé au pare-feu.
            # Sa propre documentation le décrit comme simple, non audité et sensible à la
            # charge — il n'a rien à faire sur l'Internet public.
            HTTP_SRV_PORT=8888

            # Pseudo-terminal de commandes runtime, par lequel l'application signe les
            # demandes de certificat (CA SIGN) et bloque temporairement un nœud (NODE BLOCK).
            # Sans lui, une demande déposée dans pending_csrs/ y reste indéfiniment et le
            # nœud demandeur enchaîne les « Access denied ».
            #
            # Aucun CERT_CA_HOOK n'est déclaré : la signature est une décision humaine, prise
            # depuis la page Certificats. Le hook de développement dev-ca-hook.sh signe, lui,
            # n'importe quel indicatif — il n'a sa place que dans la stack Docker.
            COMMAND_PTY=/tmp/reflector_ctrl

            # Talkgroup auquel sont rattachés les nœuds en protocole V1/V2 : ceux-ci ne
            # savent pas sélectionner de talkgroup eux-mêmes. Sans cette variable ils ne
            # participent à aucun TG et restent muets dès qu'un talkgroup est utilisé — ce
            # qui arrive dès qu'un nœud V3 est présent. C'est le paramètre clé de la
            # coexistence V2/V3 pendant la migration du parc.
            #
            # Sur un réflecteur local le numéro est libre ; celui-ci est aussi celui de la
            # stack de test, pour que tout le projet parle du même talkgroup.
            TG_FOR_V1_CLIENTS=240

            # Plage dans laquelle le réflecteur tire un talkgroup lors d'un QSY aléatoire.
            # Sans elle, le QSY aléatoire — et donc AUTO_QSY_AFTER — ne fonctionne pas.
            #
            # Syntaxe : <borne basse>:<nombre de TG>, et non une plage à tiret. La convention
            # recommandée par svxreflector.conf(5) est <MCC>9900:100 :
            #   Suisse : 2289900:100      France : 2089900:100
            RANDOM_QSY_RANGE=2289900:100

            # Protection contre un émetteur resté bloqué : au-delà de SQL_TIMEOUT secondes
            # d'émission continue, l'audio du nœud est coupé, puis il reste muet pendant
            # SQL_TIMEOUT_BLOCKTIME secondes. Sans cela, un micro coincé monopolise le
            # talkgroup jusqu'à intervention.
            SQL_TIMEOUT=300
            SQL_TIMEOUT_BLOCKTIME=60

            # Filtrages facultatifs, décommenter au besoin :
            #   ACCEPT_CERT_EMAIL — n'accepte les demandes de certificat que si l'adresse
            #                       déclarée correspond à cette expression régulière.
            #   REJECT_CALLSIGN   — refuse les indicatifs correspondants, avant tout examen.
            #ACCEPT_CERT_EMAIL=.*@example\.org$
            #REJECT_CALLSIGN=^(XX1ABC|YY2DEF)$

            [ROOT_CA]
            COMMON_NAME=SvxReflector Root CA
            COUNTRY=CH

            [ISSUING_CA]
            COMMON_NAME=SvxReflector Issuing CA
            COUNTRY=CH

            [SERVER_CERT]
            COMMON_NAME=svxreflector
            SUBJECT_ALT_NAME=DNS:localhost,IP:127.0.0.1

            # ── Talkgroups ────────────────────────────────────────────────────────────
            #
            # Une section [TG#<numéro>] par talkgroup à déclarer. Trois variables :
            #
            #   AUTO_QSY_AFTER — déplace vers un talkgroup tiré dans RANDOM_QSY_RANGE une
            #                    conversation qui dure plus de N secondes. Sert à garder
            #                    libre un canal d'appel. 0 désactive.
            #   ALLOW          — expression régulière des indicatifs autorisés sur ce
            #                    talkgroup.
            #   SHOW_ACTIVITY  — 0 masque l'activité de ce talkgroup dans le statut publié.

            # TG 0 : « aucun talkgroup ». Un nœud qui n'en a sélectionné aucun s'y trouve.
            [TG#0]
            AUTO_QSY_AFTER=0
            ALLOW=.*
            SHOW_ACTIVITY=1

            # Talkgroup des nœuds V1/V2, cf. TG_FOR_V1_CLIENTS ci-dessus. Il doit exister,
            # sans quoi la variable ne désigne rien.
            [TG#240]
            AUTO_QSY_AFTER=0
            ALLOW=.*
            SHOW_ACTIVITY=1
            """;
    }
}
