using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.SA818.UpdateSA818Configuration;

/// <summary>
/// Commande pour mettre à jour la configuration globale du module SA818.
/// </summary>
public record UpdateSA818ConfigurationCommand(
    int Volume,
    int Squelch,
    SA818Bandwidth Bandwidth,
    bool PreEmph,
    bool HighPass,
    bool LowPass) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande UpdateSA818ConfigurationCommand.
/// </summary>
public class UpdateSA818ConfigurationCommandHandler : IRequestHandler<UpdateSA818ConfigurationCommand, Validation<Error, Unit>>
{
    private readonly ISA818Repository _repository;

    public UpdateSA818ConfigurationCommandHandler(ISA818Repository repository)
    {
        _repository = repository;
    }

    public async Task<Validation<Error, Unit>> Handle(
        UpdateSA818ConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        var aggregateResult = await _repository.GetAsync(cancellationToken);

        return await aggregateResult.Match(
            Succ: async existing =>
            {
                var updateResult = existing.UpdateConfiguration(
                    command.Volume,
                    command.Squelch,
                    command.Bandwidth,
                    command.PreEmph,
                    command.HighPass,
                    command.LowPass);

                if (updateResult.IsFail)
                    return updateResult;

                return await _repository.SaveAsync(existing, cancellationToken);
            },
            Fail: async errors =>
            {
                var createResult = SA818Aggregate.Create(
                    command.Volume,
                    command.Squelch,
                    command.Bandwidth,
                    command.PreEmph,
                    command.HighPass,
                    command.LowPass);

                if (createResult.IsFail)
                    return createResult.Map(_ => unit);

                var newAggregate = createResult.Match(
                    Succ: a => a,
                    Fail: _ => throw new InvalidOperationException("Should not happen"));

                return await _repository.SaveAsync(newAggregate, cancellationToken);
            }
        );
    }
}
