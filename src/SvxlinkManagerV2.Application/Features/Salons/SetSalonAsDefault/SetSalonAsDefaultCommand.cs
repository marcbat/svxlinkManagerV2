using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.SetSalonAsDefault;

/// <summary>
/// Commande pour désigner un Salon comme salon par défaut.
/// </summary>
/// <param name="Id">Identifiant unique du salon à définir par défaut</param>
public record SetSalonAsDefaultCommand(Guid Id) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande SetSalonAsDefaultCommand
/// </summary>
public class SetSalonAsDefaultCommandHandler : IRequestHandler<SetSalonAsDefaultCommand, Validation<Error, Unit>>
{
    private readonly ISalonRepository _repository;

    public SetSalonAsDefaultCommandHandler(ISalonRepository repository)
    {
        _repository = repository;
    }

    public async Task<Validation<Error, Unit>> Handle(
        SetSalonAsDefaultCommand command,
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

        if (aggregate.IsDefault)
            return unit.ToSuccess();

        var currentDefault = await _repository.GetDefaultAsync(cancellationToken);
        if (currentDefault != null)
        {
            var unsetResult = currentDefault.UnsetDefault();
            if (unsetResult.IsFail)
                return unsetResult;

            var saveOldResult = await _repository.SaveAsync(currentDefault, cancellationToken);
            if (saveOldResult.IsFail)
                return saveOldResult;
        }

        var setResult = aggregate.SetAsDefault();
        if (setResult.IsFail)
            return setResult;

        return await _repository.SaveAsync(aggregate, cancellationToken);
    }
}
