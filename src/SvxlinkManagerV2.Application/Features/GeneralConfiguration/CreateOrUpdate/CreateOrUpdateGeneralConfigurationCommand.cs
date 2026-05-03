using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.GeneralConfiguration.CreateOrUpdate;

/// <summary>
/// Commande pour créer ou mettre à jour la configuration générale.
/// </summary>
public record CreateOrUpdateGeneralConfigurationCommand(
    bool StartReflectorOnStartup,
    bool StartDefaultSalonOnStartup,
    decimal DefaultRxFrequency = 145.550m,
    decimal DefaultTxFrequency = 145.550m) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour CreateOrUpdateGeneralConfigurationCommand.
/// </summary>
public class CreateOrUpdateGeneralConfigurationCommandHandler
    : IRequestHandler<CreateOrUpdateGeneralConfigurationCommand, Validation<Error, Unit>>
{
    private readonly IGeneralConfigurationRepository _repository;
    private readonly ILogger<CreateOrUpdateGeneralConfigurationCommandHandler> _logger;

    public CreateOrUpdateGeneralConfigurationCommandHandler(
        IGeneralConfigurationRepository repository,
        ILogger<CreateOrUpdateGeneralConfigurationCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        CreateOrUpdateGeneralConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetAsync(cancellationToken);

        if (existing is null)
        {
            var createResult = GeneralConfigurationAggregate.Create(
                command.StartReflectorOnStartup,
                command.StartDefaultSalonOnStartup,
                command.DefaultRxFrequency,
                command.DefaultTxFrequency);

            return await createResult.MatchAsync(
                async aggregate =>
                {
                    _logger.LogInformation(
                        "Création de la configuration générale : StartReflector={StartReflector}, StartDefaultSalon={StartSalon}, RxFreq={RxFreq}, TxFreq={TxFreq}",
                        command.StartReflectorOnStartup, command.StartDefaultSalonOnStartup,
                        command.DefaultRxFrequency, command.DefaultTxFrequency);

                    return await _repository.SaveAsync(aggregate, cancellationToken);
                },
                errors => Task.FromResult<Validation<Error, Unit>>(errors));
        }

        var updateResult = existing.Update(
            command.StartReflectorOnStartup,
            command.StartDefaultSalonOnStartup,
            command.DefaultRxFrequency,
            command.DefaultTxFrequency);

        return await updateResult.MatchAsync(
            async _ =>
            {
                _logger.LogInformation(
                    "Mise à jour de la configuration générale : StartReflector={StartReflector}, StartDefaultSalon={StartSalon}, RxFreq={RxFreq}, TxFreq={TxFreq}",
                    command.StartReflectorOnStartup, command.StartDefaultSalonOnStartup,
                    command.DefaultRxFrequency, command.DefaultTxFrequency);

                return await _repository.SaveAsync(existing, cancellationToken);
            },
            errors => Task.FromResult<Validation<Error, Unit>>(errors));
    }
}
