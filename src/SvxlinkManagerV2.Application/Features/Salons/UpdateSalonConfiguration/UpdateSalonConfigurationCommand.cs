using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.UpdateSalonConfiguration;

/// <summary>
/// Commande pour mettre à jour la configuration d'un Salon
/// </summary>
public record UpdateSalonConfigurationCommand(
    Guid Id,
    decimal RxFrequency,
    decimal TxFrequency,
    decimal? RxCtcss,
    decimal? TxCtcss,
    SvxLinkConfiguration Configuration);

/// <summary>
/// Handler pour la commande UpdateSalonConfigurationCommand
/// </summary>
public static class UpdateSalonConfigurationCommandHandler
{
    public static async Task<Validation<Error, Unit>> Handle(
        UpdateSalonConfigurationCommand command,
        ISalonRepository repository,
        IActiveSessionTracker tracker,
        CancellationToken cancellationToken)
    {
        if (tracker.IsSalonActive(command.Id))
            return Error.Validation("SALON_ACTIVE", "Impossible de modifier la configuration d'un salon actif").ToFailure<Unit>();

        var aggregateResult = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        var configurationWithRadio = command.Configuration with
        {
            RxFrequency = command.RxFrequency,
            TxFrequency = command.TxFrequency,
            RxCtcss = command.RxCtcss,
            TxCtcss = command.TxCtcss
        };

        var updateResult = aggregate.UpdateConfiguration(configurationWithRadio);
        if (updateResult.IsFail)
            return updateResult;

        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
