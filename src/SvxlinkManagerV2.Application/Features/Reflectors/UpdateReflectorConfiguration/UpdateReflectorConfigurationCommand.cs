using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Reflectors.UpdateReflectorConfiguration;

/// <summary>
/// Commande pour mettre à jour la configuration d'un Reflector existant.
/// Bloquée si le reflector est actif — il faut d'abord l'arrêter.
/// </summary>
/// <param name="Id">Identifiant du reflector à mettre à jour</param>
/// <param name="Name">Nouveau nom descriptif</param>
/// <param name="Config">Nouveau contenu brut INI de svxreflector.conf</param>
public record UpdateReflectorConfigurationCommand(Guid Id, string Name, string Config);

/// <summary>
/// Handler pour la commande UpdateReflectorConfigurationCommand
/// </summary>
public static class UpdateReflectorConfigurationCommandHandler
{
    public static async Task<Validation<Error, Unit>> Handle(
        UpdateReflectorConfigurationCommand command,
        IReflectorRepository repository,
        IActiveSessionTracker tracker,
        CancellationToken cancellationToken)
    {
        if (tracker.IsReflectorActive(command.Id))
            return Error.Validation("REFLECTOR_ACTIVE", "Impossible de modifier la configuration d'un reflector actif").ToFailure<Unit>();

        var aggregateResult = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        var updateResult = aggregate.UpdateConfiguration(command.Name, command.Config);
        if (updateResult.IsFail)
            return updateResult;

        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
