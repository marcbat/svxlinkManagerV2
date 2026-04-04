using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.Salons.UpdateDtmfCode;

/// <summary>
/// Commande pour mettre à jour le code DTMF d'un salon
/// </summary>
/// <param name="SalonId">Identifiant du salon</param>
/// <param name="DtmfCode">Code DTMF (null pour supprimer, 1-9999 pour définir)</param>
public record UpdateSalonDtmfCodeCommand(Guid SalonId, int? DtmfCode) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande UpdateSalonDtmfCodeCommand.
/// Valide l'unicité du code DTMF parmi les salons existants.
/// </summary>
public class UpdateSalonDtmfCodeCommandHandler : IRequestHandler<UpdateSalonDtmfCodeCommand, Validation<Error, Unit>>
{
    private readonly ISalonRepository _repository;
    private readonly ILogger<UpdateSalonDtmfCodeCommandHandler> _logger;

    public UpdateSalonDtmfCodeCommandHandler(
        ISalonRepository repository,
        ILogger<UpdateSalonDtmfCodeCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        UpdateSalonDtmfCodeCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Mise à jour du code DTMF du salon {SalonId} vers {DtmfCode}",
            command.SalonId, command.DtmfCode?.ToString() ?? "null");

        // Charger le salon
        var aggregateResult = await _repository.GetByIdAsync(command.SalonId, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        // Vérifier l'unicité du code DTMF (si non-null)
        if (command.DtmfCode.HasValue)
        {
            var allSalons = await _repository.GetAllAsync(cancellationToken);
            var existingSalon = allSalons.FirstOrDefault(s =>
                s.DtmfCode == command.DtmfCode.Value && s.Id != command.SalonId);

            if (existingSalon != null)
            {
                _logger.LogWarning("Le code DTMF {DtmfCode} est déjà utilisé par le salon {SalonName}",
                    command.DtmfCode.Value, existingSalon.Name);
                return Error.Validation("DTMF_CODE_ALREADY_USED",
                    $"Le code DTMF {command.DtmfCode.Value} est déjà utilisé par le salon '{existingSalon.Name}'")
                    .ToFailure<Unit>();
            }
        }

        // Mettre à jour le code DTMF sur l'agrégat
        var updateResult = aggregate.UpdateDtmfCode(command.DtmfCode);
        if (updateResult.IsFail)
            return updateResult;

        // Sauvegarder
        var saveResult = await _repository.SaveAsync(aggregate, cancellationToken);
        if (saveResult.IsFail)
            return saveResult;

        _logger.LogInformation("Code DTMF du salon {SalonId} mis à jour vers {DtmfCode}",
            command.SalonId, command.DtmfCode?.ToString() ?? "null");

        return unit.ToSuccess();
    }
}
