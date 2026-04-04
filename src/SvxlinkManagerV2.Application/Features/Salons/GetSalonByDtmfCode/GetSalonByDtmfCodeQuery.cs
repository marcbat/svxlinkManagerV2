using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Features.Salons.GetSalonByDtmfCode;

/// <summary>
/// Query pour rechercher un salon par son code DTMF
/// </summary>
/// <param name="DtmfCode">Code DTMF à rechercher</param>
public record GetSalonByDtmfCodeQuery(int DtmfCode) : IRequest<SalonAggregate?>;

/// <summary>
/// Handler pour la query GetSalonByDtmfCodeQuery.
/// Retourne le salon correspondant au code DTMF ou null si aucun salon trouvé.
/// </summary>
public class GetSalonByDtmfCodeQueryHandler : IRequestHandler<GetSalonByDtmfCodeQuery, SalonAggregate?>
{
    private readonly ISalonRepository _repository;
    private readonly ILogger<GetSalonByDtmfCodeQueryHandler> _logger;

    public GetSalonByDtmfCodeQueryHandler(
        ISalonRepository repository,
        ILogger<GetSalonByDtmfCodeQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SalonAggregate?> Handle(
        GetSalonByDtmfCodeQuery query,
        CancellationToken cancellationToken)
    {
        var salon = await _repository.GetByDtmfCodeAsync(query.DtmfCode, cancellationToken);

        if (salon == null)
        {
            _logger.LogDebug("Aucun salon trouvé pour le code DTMF {DtmfCode}", query.DtmfCode);
        }

        return salon;
    }
}
