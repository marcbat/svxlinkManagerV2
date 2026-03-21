using LanguageExt;
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
public record CreateReflectorCommand(Guid Id, string Name, string Config);

/// <summary>
/// Handler pour la commande CreateReflectorCommand
/// </summary>
public static class CreateReflectorCommandHandler
{
    /// <summary>
    /// Crée un nouveau Reflector dans l'event store
    /// </summary>
    public static async Task<Validation<Error, Guid>> Handle(
        CreateReflectorCommand command,
        IReflectorRepository repository,
        CancellationToken cancellationToken)
    {
        // Création de l'aggregate avec validations
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

        // Sauvegarde dans l'event store
        var saveResult = await repository.SaveAsync(aggregate, cancellationToken);

        return saveResult.Match(
            Succ: _ => Validation<Error, Guid>.Success(aggregate.Id),
            Fail: errors => Validation<Error, Guid>.Fail(errors));
    }
}
