using LanguageExt;
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
    bool StartDefaultSalonOnStartup);

/// <summary>
/// Handler pour CreateOrUpdateGeneralConfigurationCommand.
/// Crée la configuration si elle n'existe pas, sinon la met à jour.
/// </summary>
public static class CreateOrUpdateGeneralConfigurationCommandHandler
{
    public static async Task<Validation<Error, Unit>> HandleAsync(
        CreateOrUpdateGeneralConfigurationCommand command,
        IGeneralConfigurationRepository repository,
        ILogger<CreateOrUpdateGeneralConfigurationCommand> logger,
        CancellationToken ct = default)
    {
        var existing = await repository.GetAsync(ct);

        if (existing is null)
        {
            var createResult = GeneralConfigurationAggregate.Create(
                command.StartReflectorOnStartup,
                command.StartDefaultSalonOnStartup);

            return await createResult.MatchAsync(
                async aggregate =>
                {
                    logger.LogInformation(
                        "Création de la configuration générale : StartReflector={StartReflector}, StartDefaultSalon={StartSalon}",
                        command.StartReflectorOnStartup, command.StartDefaultSalonOnStartup);

                    return await repository.SaveAsync(aggregate, ct);
                },
                errors => Task.FromResult<Validation<Error, Unit>>(errors));
        }

        var updateResult = existing.Update(
            command.StartReflectorOnStartup,
            command.StartDefaultSalonOnStartup);

        return await updateResult.MatchAsync(
            async _ =>
            {
                logger.LogInformation(
                    "Mise à jour de la configuration générale : StartReflector={StartReflector}, StartDefaultSalon={StartSalon}",
                    command.StartReflectorOnStartup, command.StartDefaultSalonOnStartup);

                return await repository.SaveAsync(existing, ct);
            },
            errors => Task.FromResult<Validation<Error, Unit>>(errors));
    }
}
