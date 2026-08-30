using LanguageExt;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.SystemControl;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Infrastructure.Runtime;

/// <summary>
/// Implémentation simulée du contrôle d'alimentation, destinée au développement sans machine cible.
/// Aucun appel système n'est effectué : les actions sont uniquement journalisées.
/// Activée via la configuration SystemControl:UseMock = true.
/// </summary>
public class SystemControlMockService : ISystemControlService
{
    private readonly ILogger<SystemControlMockService> _logger;

    public SystemControlMockService(ILogger<SystemControlMockService> logger)
    {
        _logger = logger;
    }

    public SystemControlAvailabilityDto GetAvailability()
        => new(
            IsSupported: true,
            IsSimulated: true,
            UnsupportedReason: null);

    public Task<Validation<Error, Unit>> RebootAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("MOCK: redémarrage de la machine simulé (aucun appel système effectué)");
        return Task.FromResult(Unit.Default.ToSuccess());
    }

    public Task<Validation<Error, Unit>> ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("MOCK: arrêt de la machine simulé (aucun appel système effectué)");
        return Task.FromResult(Unit.Default.ToSuccess());
    }
}
