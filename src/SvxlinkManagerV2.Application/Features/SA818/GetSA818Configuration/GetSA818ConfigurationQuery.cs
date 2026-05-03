using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.SA818.GetSA818Configuration;

/// <summary>
/// Query pour récupérer la configuration actuelle du module SA818.
/// </summary>
public record GetSA818ConfigurationQuery() : IRequest<Validation<Error, SA818ConfigurationDto>>;

/// <summary>
/// Handler pour la query GetSA818ConfigurationQuery.
/// </summary>
public class GetSA818ConfigurationQueryHandler : IRequestHandler<GetSA818ConfigurationQuery, Validation<Error, SA818ConfigurationDto>>
{
    private readonly ISA818Repository _repository;
    private readonly ISalonRepository _salonRepository;
    private readonly IActiveSessionTracker _tracker;

    public GetSA818ConfigurationQueryHandler(
        ISA818Repository repository,
        ISalonRepository salonRepository,
        IActiveSessionTracker tracker)
    {
        _repository = repository;
        _salonRepository = salonRepository;
        _tracker = tracker;
    }

    public async Task<Validation<Error, SA818ConfigurationDto>> Handle(
        GetSA818ConfigurationQuery query,
        CancellationToken cancellationToken)
    {
        var configuration = await _repository.GetConfigurationAsync(cancellationToken);

        if (configuration == null)
            return Error.NotFound("SA818", "Configuration SA818 non initialisée")
                .ToFailure<SA818ConfigurationDto>();

        var activeSalonId = _tracker.ActiveSalonId;
        if (activeSalonId.HasValue)
        {
            var salonResult = await _salonRepository.GetByIdAsync(activeSalonId.Value, cancellationToken);
            var activeSalon = salonResult.Match(
                Succ: s => s.IsDeleted ? null : s,
                Fail: _ => null);

            if (activeSalon is not null)
            {
                var salonConfig = activeSalon.Configuration;
                return (configuration with
                {
                    RxFrequency = salonConfig.RxFrequency,
                    TxFrequency = salonConfig.TxFrequency,
                    RxCtcss = salonConfig.RxCtcss,
                    TxCtcss = salonConfig.TxCtcss
                }).ToSuccess();
            }
        }

        return configuration.ToSuccess();
    }
}
