using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.CreateSalon;

/// <summary>
/// Commande pour créer un nouveau Salon
/// </summary>
public record CreateSalonCommand(
    Guid Id,
    string Name,
    bool IsDefault,
    bool IsTemporized,
    decimal RxFrequency,
    decimal TxFrequency,
    decimal? RxCtcss,
    decimal? TxCtcss,
    SvxLinkConfiguration Configuration) : IRequest<Validation<Error, Guid>>;

/// <summary>
/// Handler pour la commande CreateSalonCommand
/// </summary>
public class CreateSalonCommandHandler : IRequestHandler<CreateSalonCommand, Validation<Error, Guid>>
{
    private readonly ISalonRepository _repository;

    public CreateSalonCommandHandler(ISalonRepository repository)
    {
        _repository = repository;
    }

    public async Task<Validation<Error, Guid>> Handle(
        CreateSalonCommand command,
        CancellationToken cancellationToken)
    {
        var configurationWithRadio = command.Configuration with
        {
            RxFrequency = command.RxFrequency,
            TxFrequency = command.TxFrequency,
            RxCtcss = command.RxCtcss,
            TxCtcss = command.TxCtcss
        };

        var aggregateResult = SalonAggregate.Create(
            command.Id,
            command.Name,
            command.IsDefault,
            command.IsTemporized,
            configurationWithRadio);

        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Guid>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        if (command.IsDefault)
        {
            var currentDefault = await _repository.GetDefaultAsync(cancellationToken);
            if (currentDefault != null)
            {
                var unsetResult = currentDefault.UnsetDefault();
                if (unsetResult.IsFail)
                    return unsetResult.Match(
                        Succ: _ => throw new InvalidOperationException(),
                        Fail: errors => Validation<Error, Guid>.Fail(errors));

                var saveOldResult = await _repository.SaveAsync(currentDefault, cancellationToken);
                if (saveOldResult.IsFail)
                    return saveOldResult.Match(
                        Succ: _ => throw new InvalidOperationException(),
                        Fail: errors => Validation<Error, Guid>.Fail(errors));
            }
        }

        var saveResult = await _repository.SaveAsync(aggregate, cancellationToken);

        return saveResult.Match(
            Succ: _ => Validation<Error, Guid>.Success(aggregate.Id),
            Fail: errors => Validation<Error, Guid>.Fail(errors));
    }
}
