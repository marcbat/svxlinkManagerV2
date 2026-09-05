using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Features.Statistics;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.Statistics;

/// <summary>
/// Applique la rétention de l'historique d'activité : une passe au démarrage, puis à intervalle
/// régulier. Sans elle, la table d'événements croîtrait sans fin sur une carte SD.
/// </summary>
public class StatisticsPurgeHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StatisticsOptions _options;
    private readonly ILogger<StatisticsPurgeHostedService> _logger;

    public StatisticsPurgeHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<StatisticsOptions> options,
        ILogger<StatisticsPurgeHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Une rétention nulle ou négative désactive la purge : l'opérateur qui veut tout
        // garder ne doit pas voir son historique disparaître à cause d'une faute de frappe.
        if (_options.RetentionDays <= 0)
        {
            _logger.LogInformation("Purge de l'historique d'activité désactivée (rétention {Days} jour(s))",
                _options.RetentionDays);
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, _options.PurgeIntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            await PurgeAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

            var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays);
            var result = await repository.PurgeBeforeAsync(cutoff, cancellationToken);

            result.Match(
                Succ: deleted =>
                {
                    if (deleted > 0)
                        _logger.LogInformation(
                            "Historique d'activité : {Count} enregistrement(s) antérieur(s) à {Cutoff:u} purgé(s)",
                            deleted, cutoff);
                    return LanguageExt.Unit.Default;
                },
                Fail: errors =>
                {
                    _logger.LogWarning(
                        "Historique d'activité : échec de la purge — {Errors}",
                        string.Join(", ", errors.Select(e => e.Message)));
                    return LanguageExt.Unit.Default;
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historique d'activité : échec de la purge");
        }
    }
}
