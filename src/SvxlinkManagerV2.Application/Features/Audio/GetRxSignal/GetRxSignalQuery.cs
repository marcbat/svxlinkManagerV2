using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Audio.GetRxSignal;

/// <summary>
/// Query retournant les indicateurs du signal reçu : RSSI du module SA818 et écrêtages
/// d'entrée signalés par SVXLink. Destinée à un rafraîchissement périodique de la page audio.
/// </summary>
public record GetRxSignalQuery() : IRequest<Validation<Error, RxSignalDto>>;

/// <summary>
/// Handler de <see cref="GetRxSignalQuery"/>.
/// Un module muet n'est pas un échec : le motif est porté par le DTO, le compteur d'écrêtages
/// restant exploitable.
/// </summary>
public class GetRxSignalQueryHandler : IRequestHandler<GetRxSignalQuery, Validation<Error, RxSignalDto>>
{
    private readonly ISA818Service _sa818Service;
    private readonly IRxDistortionService _distortionService;

    public GetRxSignalQueryHandler(
        ISA818Service sa818Service,
        IRxDistortionService distortionService)
    {
        _sa818Service = sa818Service;
        _distortionService = distortionService;
    }

    public async Task<Validation<Error, RxSignalDto>> Handle(
        GetRxSignalQuery query,
        CancellationToken cancellationToken)
    {
        var rssiResult = await _sa818Service.ReadRssiAsync(cancellationToken);

        var dto = rssiResult.Match(
            Succ: rssi => new RxSignalDto(
                rssi,
                RssiError: null,
                _distortionService.LastDetectedAt,
                _distortionService.DetectionCount),
            Fail: _ => new RxSignalDto(
                Rssi: null,
                RssiError: "Le module SA818 n'a pas rendu de niveau de signal.",
                _distortionService.LastDetectedAt,
                _distortionService.DetectionCount));

        return dto.ToSuccess();
    }
}
