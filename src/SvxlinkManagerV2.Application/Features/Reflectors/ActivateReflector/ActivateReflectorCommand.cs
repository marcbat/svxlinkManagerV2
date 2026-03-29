using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Reflectors.ActivateReflector;

/// <summary>
/// Commande pour activer le Reflector (démarre le daemon svxreflector).
/// </summary>
/// <param name="Id">Identifiant unique du reflector à activer</param>
public record ActivateReflectorCommand(Guid Id) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande ActivateReflectorCommand.
/// </summary>
public class ActivateReflectorCommandHandler : IRequestHandler<ActivateReflectorCommand, Validation<Error, Unit>>
{
    private const string ReflectorConfigPath = "/etc/svxlink/svxreflector.conf";

    private readonly IReflectorRepository _repository;
    private readonly IActiveSessionTracker _tracker;
    private readonly IReflectorConfigurationService _configurationService;
    private readonly IReflectorDaemonService _daemonService;
    private readonly ILogger<ActivateReflectorCommandHandler> _logger;

    public ActivateReflectorCommandHandler(
        IReflectorRepository repository,
        IActiveSessionTracker tracker,
        IReflectorConfigurationService configurationService,
        IReflectorDaemonService daemonService,
        ILogger<ActivateReflectorCommandHandler> logger)
    {
        _repository = repository;
        _tracker = tracker;
        _configurationService = configurationService;
        _daemonService = daemonService;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        ActivateReflectorCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activation du Reflector {ReflectorId}", command.Id);

        var aggregateResult = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        if (aggregate.IsDeleted)
            return Error.Validation("REFLECTOR_DELETED", "Le reflector est supprimé").ToFailure<Unit>();

        _logger.LogInformation("Ecriture du fichier {Path}", ReflectorConfigPath);
        var configResult = await _configurationService.WriteConfigAsync(aggregate, ReflectorConfigPath, cancellationToken);
        if (configResult.IsFail)
            return Error.Validation("REFLECTOR_CONFIG_ERROR", "Impossible d'écrire le fichier svxreflector.conf").ToFailure<Unit>();

        _logger.LogInformation("Démarrage du daemon svxreflector");
        var daemonResult = await _daemonService.RestartAsync(cancellationToken);
        if (daemonResult.IsFail)
            return Error.Validation("REFLECTOR_DAEMON_ERROR", "Impossible de démarrer le daemon svxreflector").ToFailure<Unit>();

        _tracker.SetActiveReflector(command.Id);

        _logger.LogInformation("Reflector {ReflectorName} ({ReflectorId}) activé avec succès", aggregate.Name, command.Id);
        return unit.ToSuccess();
    }
}
