using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;
using SvxlinkManagerV2.Application.Features.Reflectors;
using SvxlinkManagerV2.Application.Features.Reflectors.SignCertificateRequest;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.Reflector;

/// <summary>
/// Signe automatiquement les demandes de certificat en attente, <b>quand et seulement quand</b>
/// la configuration l'autorise explicitement.
/// </summary>
/// <remarks>
/// <para>
/// Ce service remplace le hook shell d'auto-signature du développement
/// (<c>dev-ca-hook.sh</c>) pour les installations qui en veulent un : livrer ce script sur
/// une machine de production reviendrait à y installer un signataire aveugle que rien
/// n'inhibe. Ici l'auto-signature est un réglage nommé, désactivé par défaut, et chaque
/// signature laisse une trace dans le journal.
/// </para>
/// <para>
/// Il n'en reste pas moins <b>réservé au développement</b> : il signe sans vérifier
/// l'identité du demandeur, ce qui n'est acceptable que sur un réflecteur inaccessible
/// depuis l'extérieur.
/// </para>
/// </remarks>
public class CertificateAutoSignHostedService : BackgroundService
{
    private readonly ILogger<CertificateAutoSignHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPendingCertificateRequestReader _reader;
    private readonly CertificateAuthorityOptions _options;

    public CertificateAutoSignHostedService(
        ILogger<CertificateAutoSignHostedService> logger,
        IServiceScopeFactory scopeFactory,
        IPendingCertificateRequestReader reader,
        IOptions<CertificateAuthorityOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _reader = reader;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoSign)
        {
            _logger.LogInformation(
                "Auto-signature des certificats désactivée : les demandes attendent une décision explicite");
            return;
        }

        _logger.LogWarning(
            "AUTO-SIGNATURE DES CERTIFICATS ACTIVE — toute demande sera signée sans vérification. " +
            "Réservé au développement : à ne pas laisser sur un réflecteur joignable depuis l'extérieur.");

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.AutoSignIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await SignPendingRequestsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Une boucle de fond ne doit jamais emporter l'hôte avec elle.
                _logger.LogError(ex, "Erreur lors de l'auto-signature des demandes de certificat");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SignPendingRequestsAsync(CancellationToken cancellationToken)
    {
        var pending = await _reader.ListAsync(cancellationToken);

        var requests = pending.Match(
            Succ: list => list,
            Fail: _ => []);

        if (requests.Count == 0)
            return;

        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        foreach (var request in requests)
        {
            _logger.LogWarning(
                "Auto-signature de la demande de {Callsign} ({Email})",
                request.Callsign, request.Email ?? "sans adresse");

            var result = await mediator.Send(new SignCertificateRequestCommand(request.Callsign), cancellationToken);

            result.IfFail(errors => _logger.LogError(
                "Échec de l'auto-signature de {Callsign} : {Errors}",
                request.Callsign, string.Join(", ", errors.Select(e => e.Message))));
        }
    }
}
