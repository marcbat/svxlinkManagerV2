using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.Statistics.ResetStatistics;

/// <summary>
/// Commande de remise à zéro de l'historique d'activité.
/// </summary>
public record ResetStatisticsCommand() : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler de <see cref="ResetStatisticsCommand"/>.
/// Vide les deux tables, session en cours comprise : le recorder rouvrira une session
/// à la prochaine activation de salon.
/// </summary>
public class ResetStatisticsCommandHandler : IRequestHandler<ResetStatisticsCommand, Validation<Error, Unit>>
{
    private readonly IActivityRepository _repository;
    private readonly ILogger<ResetStatisticsCommandHandler> _logger;

    public ResetStatisticsCommandHandler(
        IActivityRepository repository,
        ILogger<ResetStatisticsCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        ResetStatisticsCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Remise à zéro de l'historique d'activité demandée");

        return await _repository.ResetAsync(cancellationToken);
    }
}
