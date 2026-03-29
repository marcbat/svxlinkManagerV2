using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Sounds.UpdateSound;

/// <summary>
/// Commande pour mettre à jour un Sound existant
/// </summary>
public record UpdateSoundCommand(
    Guid Id,
    string? Name = null,
    byte[]? FileContent = null) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande UpdateSoundCommand
/// </summary>
public class UpdateSoundCommandHandler : IRequestHandler<UpdateSoundCommand, Validation<Error, Unit>>
{
    private readonly ISoundRepository _repository;

    public UpdateSoundCommandHandler(ISoundRepository repository)
    {
        _repository = repository;
    }

    public async Task<Validation<Error, Unit>> Handle(
        UpdateSoundCommand command,
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

        var updateResult = aggregate.Update(command.Name, command.FileContent);

        if (updateResult.IsFail)
            return updateResult;

        return await _repository.SaveAsync(aggregate, cancellationToken);
    }
}
