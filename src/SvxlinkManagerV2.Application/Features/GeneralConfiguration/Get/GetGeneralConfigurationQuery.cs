using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;

namespace SvxlinkManagerV2.Application.Features.GeneralConfiguration.Get;

/// <summary>
/// Query pour récupérer la configuration générale.
/// </summary>
public record GetGeneralConfigurationQuery() : IRequest<GeneralConfigurationAggregate?>;

/// <summary>
/// Handler pour GetGeneralConfigurationQuery.
/// </summary>
public class GetGeneralConfigurationQueryHandler : IRequestHandler<GetGeneralConfigurationQuery, GeneralConfigurationAggregate?>
{
    private readonly IGeneralConfigurationRepository _repository;

    public GetGeneralConfigurationQueryHandler(IGeneralConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<GeneralConfigurationAggregate?> Handle(
        GetGeneralConfigurationQuery query,
        CancellationToken cancellationToken)
    {
        return await _repository.GetAsync(cancellationToken);
    }
}
