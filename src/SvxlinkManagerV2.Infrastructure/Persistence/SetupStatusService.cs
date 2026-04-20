using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Implémentation de <see cref="ISetupStatusService"/> basée sur la présence de salons en base.
/// Le résultat est mis en cache en mémoire pour éviter des requêtes répétées à chaque navigation.
/// Singleton : le cache est partagé entre tous les circuits SignalR.
/// </summary>
public class SetupStatusService : ISetupStatusService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SetupStatusService> _logger;
    private bool? _cachedResult;

    public SetupStatusService(
        IServiceScopeFactory scopeFactory,
        ILogger<SetupStatusService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedResult.HasValue)
        {
            _logger.LogDebug("SetupStatusService: résultat en cache ({Result}).", _cachedResult.Value);
            return _cachedResult.Value;
        }

        using var scope = _scopeFactory.CreateScope();
        var salonRepository = scope.ServiceProvider.GetRequiredService<ISalonRepository>();

        var salons = await salonRepository.GetAllAsync(cancellationToken);
        var reflectorSalonCount = salons.Count(s => s.SalonType != SalonType.Parrot);
        var required = reflectorSalonCount == 0;

        _cachedResult = required;
        _logger.LogInformation(
            "SetupStatusService: setup requis = {Required} ({Count} salon(s) réflecteur en base, {Total} total).",
            required,
            reflectorSalonCount,
            salons.Count);

        return required;
    }

    /// <inheritdoc/>
    public void InvalidateCache()
    {
        _logger.LogInformation("SetupStatusService: invalidation du cache.");
        _cachedResult = null;
    }
}
