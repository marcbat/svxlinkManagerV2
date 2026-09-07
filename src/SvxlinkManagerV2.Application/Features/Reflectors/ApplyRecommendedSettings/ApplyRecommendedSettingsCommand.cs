using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Reflectors.ApplyRecommendedSettings;

/// <summary>
/// Ajoute à la configuration du réflecteur les réglages recommandés qu'elle ne déclare pas.
/// </summary>
/// <param name="Id">Identifiant du réflecteur à compléter.</param>
/// <remarks>
/// Le seeder ne crée le réflecteur que s'il n'en existe aucun : une installation déjà en
/// service ne reçoit jamais les clés ajoutées au modèle par la suite. Cette commande est le
/// chemin par lequel elle les obtient, sans perdre ses propres réglages ni ses commentaires.
/// </remarks>
public record ApplyRecommendedSettingsCommand(Guid Id) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler de <see cref="ApplyRecommendedSettingsCommand"/>.
/// </summary>
public class ApplyRecommendedSettingsCommandHandler
    : IRequestHandler<ApplyRecommendedSettingsCommand, Validation<Error, Unit>>
{
    private readonly IReflectorRepository _repository;
    private readonly IActiveSessionTracker _tracker;
    private readonly ILogger<ApplyRecommendedSettingsCommandHandler> _logger;

    public ApplyRecommendedSettingsCommandHandler(
        IReflectorRepository repository,
        IActiveSessionTracker tracker,
        ILogger<ApplyRecommendedSettingsCommandHandler> logger)
    {
        _repository = repository;
        _tracker = tracker;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        ApplyRecommendedSettingsCommand command,
        CancellationToken cancellationToken)
    {
        // Le démon relit sa configuration au démarrage : la modifier à chaud donnerait une
        // vue divergente entre ce qui tourne et ce qui est enregistré.
        if (_tracker.IsReflectorActive(command.Id))
            return Error.Validation(
                "REFLECTOR_ACTIVE",
                "Arrêtez le réflecteur avant de compléter sa configuration")
                .ToFailure<Unit>();

        var aggregateResult = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: Validation<Error, Unit>.Fail);

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        var missing = ReflectorRecommendedSettings.MissingFrom(aggregate.Config);
        if (missing.Count == 0)
            return unit.ToSuccess();

        _logger.LogInformation(
            "Ajout de {Count} réglage(s) recommandé(s) à la configuration du réflecteur : {Keys}",
            missing.Count, string.Join(", ", missing.Select(setting => setting.Key)));

        var merged = ReflectorConfigurationMerger.Add(aggregate.Config, missing);

        var updateResult = aggregate.UpdateConfiguration(aggregate.Name, merged);
        if (updateResult.IsFail)
            return updateResult;

        return await _repository.SaveAsync(aggregate, cancellationToken);
    }
}
