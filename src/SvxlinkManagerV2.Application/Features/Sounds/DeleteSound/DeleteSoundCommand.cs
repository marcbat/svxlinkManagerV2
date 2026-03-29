using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Sounds.DeleteSound;

/// <summary>
/// Commande pour supprimer un Sound
/// </summary>
/// <param name="Id">Identifiant du Sound à supprimer</param>
public record DeleteSoundCommand(Guid Id) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande DeleteSoundCommand
/// </summary>
public class DeleteSoundCommandHandler : IRequestHandler<DeleteSoundCommand, Validation<Error, Unit>>
{
    private readonly ISoundRepository _repository;

    public DeleteSoundCommandHandler(ISoundRepository repository)
    {
        _repository = repository;
    }

    public async Task<Validation<Error, Unit>> Handle(
        DeleteSoundCommand command,
        CancellationToken cancellationToken)
    {
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
