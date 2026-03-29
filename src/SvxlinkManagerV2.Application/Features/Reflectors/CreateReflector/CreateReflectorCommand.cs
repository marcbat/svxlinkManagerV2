using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Reflectors.CreateReflector;

/// <summary>
/// Commande pour créer un nouveau Reflector
/// </summary>
/// <param name="Id">Identifiant unique du reflector</param>
/// <param name="Name">Nom descriptif du reflector</param>
/// <param name="Config">Contenu brut INI de svxreflector.conf</param>
public record CreateReflectorCommand(Guid Id, string Name, string Config) : IRequest<Validation<Error, Guid>>;

/// <summary>
/// Handler pour la commande CreateReflectorCommand
/// </summary>
public class CreateReflectorCommandHandler : IRequestHandler<CreateReflectorCommand, Validation<Error, Guid>>
{
    private readonly IReflectorRepository _repository;

    public CreateReflectorCommandHandler(IReflectorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Validation<Error, Guid>> Handle(
        CreateReflectorCommand command,
        CancellationToken cancellationToken)
    {
        var aggregateResult = ReflectorAggregate.Create(
            command.Id,
            command.Name,
            command.Config);

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
