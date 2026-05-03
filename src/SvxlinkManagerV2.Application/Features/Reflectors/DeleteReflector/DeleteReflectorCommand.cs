using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Reflectors.DeleteReflector;

/// <summary>
/// Commande pour supprimer (soft delete) un Reflector.
/// </summary>
/// <param name="Id">Identifiant du reflector à supprimer</param>
public record DeleteReflectorCommand(Guid Id) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande DeleteReflectorCommand
/// </summary>
public class DeleteReflectorCommandHandler : IRequestHandler<DeleteReflectorCommand, Validation<Error, Unit>>
{
    private readonly IReflectorRepository _repository;
    private readonly IActiveSessionTracker _tracker;

    public DeleteReflectorCommandHandler(IReflectorRepository repository, IActiveSessionTracker tracker)
    {
        _repository = repository;
        _tracker = tracker;
    }

    public async Task<Validation<Error, Unit>> Handle(
        DeleteReflectorCommand command,
        CancellationToken cancellationToken)
    {
        if (_tracker.IsReflectorActive(command.Id))
            return Error.Validation("REFLECTOR_ACTIVE", "Impossible de supprimer un reflector actif").ToFailure<Unit>();

        return await _repository.DeleteAsync(command.Id, cancellationToken);
    }
}
