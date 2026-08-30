using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Infrastructure.Hardware;

/// <summary>
/// Test d'émission simulé : la machine à états et le minuteur de relâchement fonctionnent
/// à l'identique, mais aucun GPIO n'est touché et aucune porteuse n'est émise.
/// </summary>
public class PttTestMockService : PttTestServiceBase
{
    private readonly ILogger<PttTestMockService> _logger;

    public PttTestMockService(IOptions<AudioOptions> options, ILogger<PttTestMockService> logger)
        : base(options, logger)
    {
        _logger = logger;
    }

    protected override bool IsSimulated => true;

    protected override Task<Validation<Error, Unit>> SetPttAsync(bool keyed, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MOCK: PTT {State}", keyed ? "activé" : "relâché");

        return Task.FromResult(Unit.Default.ToSuccess());
    }
}
