using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.DeleteSalon;

/// <summary>
/// Commande pour supprimer un Salon (soft delete)
/// </summary>
public record DeleteSalonCommand(Guid Id) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande DeleteSalonCommand
/// </summary>
public class DeleteSalonCommandHandler : IRequestHandler<DeleteSalonCommand, Validation<Error, Unit>>
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;

    public DeleteSalonCommandHandler(
        ISalonRepository repository,
        IActiveSessionTracker tracker)
    {
        _repository = repository;
        _tracker = tracker;
    }

    public async Task<Validation<Error, Unit>> Handle(
        DeleteSalonCommand command,
        CancellationToken cancellationToken)
    {
        if (_tracker.IsSalonActive(command.Id))
            return Error.Validation("SALON_ACTIVE", "Impossible de supprimer un salon actif").ToFailure<Unit>();

        var aggregateResult = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        var deleteResult = aggregate.Delete();
        if (deleteResult.IsFail)
            return deleteResult;

        return await _repository.SaveAsync(aggregate, cancellationToken);
    }
}
