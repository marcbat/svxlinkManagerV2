using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.UpdateSalonConfiguration;

/// <summary>
/// Commande pour mettre à jour la configuration d'un Salon
/// </summary>
public record UpdateSalonConfigurationCommand(
    Guid Id,
    decimal RxFrequency,
    decimal TxFrequency,
    decimal? RxCtcss,
    decimal? TxCtcss,
    SvxLinkConfiguration Configuration) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande UpdateSalonConfigurationCommand
/// </summary>
public class UpdateSalonConfigurationCommandHandler : IRequestHandler<UpdateSalonConfigurationCommand, Validation<Error, Unit>>
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;

    public UpdateSalonConfigurationCommandHandler(ISalonRepository repository, IActiveSessionTracker tracker)
    {
        _repository = repository;
        _tracker = tracker;
    }

    public async Task<Validation<Error, Unit>> Handle(
        UpdateSalonConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        if (_tracker.IsSalonActive(command.Id))
            return Error.Validation("SALON_ACTIVE", "Impossible de modifier la configuration d'un salon actif").ToFailure<Unit>();

        var aggregateResult = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        var configurationWithRadio = command.Configuration with
        {
            RxFrequency = command.RxFrequency,
            TxFrequency = command.TxFrequency,
            RxCtcss = command.RxCtcss,
            TxCtcss = command.TxCtcss
        };

        var updateResult = aggregate.UpdateConfiguration(configurationWithRadio);
        if (updateResult.IsFail)
            return updateResult;

        return await _repository.SaveAsync(aggregate, cancellationToken);
    }
}
