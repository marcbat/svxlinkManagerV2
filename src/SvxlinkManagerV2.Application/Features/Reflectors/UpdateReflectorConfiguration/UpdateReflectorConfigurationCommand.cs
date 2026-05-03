using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Reflectors.UpdateReflectorConfiguration;

/// <summary>
/// Commande pour mettre à jour la configuration d'un Reflector existant.
/// </summary>
/// <param name="Id">Identifiant du reflector à mettre à jour</param>
/// <param name="Name">Nouveau nom descriptif</param>
/// <param name="Config">Nouveau contenu brut INI de svxreflector.conf</param>
public record UpdateReflectorConfigurationCommand(Guid Id, string Name, string Config) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande UpdateReflectorConfigurationCommand
/// </summary>
public class UpdateReflectorConfigurationCommandHandler : IRequestHandler<UpdateReflectorConfigurationCommand, Validation<Error, Unit>>
{
    private readonly IReflectorRepository _repository;
    private readonly IActiveSessionTracker _tracker;

    public UpdateReflectorConfigurationCommandHandler(IReflectorRepository repository, IActiveSessionTracker tracker)
    {
        _repository = repository;
        _tracker = tracker;
    }

    public async Task<Validation<Error, Unit>> Handle(
        UpdateReflectorConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        if (_tracker.IsReflectorActive(command.Id))
            return Error.Validation("REFLECTOR_ACTIVE", "Impossible de modifier la configuration d'un reflector actif").ToFailure<Unit>();

        var aggregateResult = await _repository.GetByIdAsync(command.Id, cancellationToken);
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

        return await _repository.SaveAsync(aggregate, cancellationToken);
    }
}
