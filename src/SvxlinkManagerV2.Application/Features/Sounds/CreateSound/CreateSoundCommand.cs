using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Sounds.CreateSound;

/// <summary>
/// Commande pour créer un nouveau Sound (fichier audio WAV)
/// </summary>
public record CreateSoundCommand(
    Guid Id,
    string Name,
    byte[] FileContent) : IRequest<Validation<Error, Guid>>;

/// <summary>
/// Handler pour la commande CreateSoundCommand
/// </summary>
public class CreateSoundCommandHandler : IRequestHandler<CreateSoundCommand, Validation<Error, Guid>>
{
    private readonly ISoundRepository _repository;

    public CreateSoundCommandHandler(ISoundRepository repository)
    {
        _repository = repository;
    }

    public async Task<Validation<Error, Guid>> Handle(
        CreateSoundCommand command,
        CancellationToken cancellationToken)
    {
        var aggregateResult = SoundAggregate.Create(
            command.Id,
            command.Name,
            command.FileContent);

        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Guid>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        var saveResult = await _repository.SaveAsync(aggregate, cancellationToken);

        return saveResult.Match(
            Succ: _ => Validation<Error, Guid>.Success(aggregate.Id),
            Fail: errors => Validation<Error, Guid>.Fail(errors));
    }
}
