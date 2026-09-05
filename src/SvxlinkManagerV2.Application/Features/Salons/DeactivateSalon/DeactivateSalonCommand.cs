using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.Salons.ActivateStandaloneMode;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Statistics;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.DeactivateSalon;

/// <summary>
/// Commande pour désactiver un Salon (déconnexion du reflector).
/// Après désactivation, SVXLink revient en mode standalone (écoute DTMF sans réflecteur),
/// identique à l'état de démarrage de l'application sans salon par défaut configuré.
/// </summary>
/// <param name="Id">Identifiant unique du salon à désactiver</param>
/// <param name="Origin">
/// Ce qui déclenche la désactivation, transmis au mode autonome qui prend le relais
/// et ouvre la session correspondante dans l'historique.
/// </param>
public record DeactivateSalonCommand(Guid Id, SalonActivationOrigin Origin = SalonActivationOrigin.Web)
    : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande DeactivateSalonCommand.
/// Valide que le salon est actif, puis délègue à ActivateStandaloneModeCommand
/// pour replacer SVXLink dans son état initial (simplex sans réflecteur).
/// </summary>
public class DeactivateSalonCommandHandler : IRequestHandler<DeactivateSalonCommand, Validation<Error, Unit>>
{
    private readonly IActiveSessionTracker _tracker;
    private readonly IMediator _mediator;
    private readonly ILogger<DeactivateSalonCommandHandler> _logger;

    public DeactivateSalonCommandHandler(
        IActiveSessionTracker tracker,
        IMediator mediator,
        ILogger<DeactivateSalonCommandHandler> logger)
    {
        _tracker = tracker;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        DeactivateSalonCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Désactivation du Salon {SalonId}", command.Id);

        if (!_tracker.IsSalonActive(command.Id))
            return Error.Validation("SALON_NOT_ACTIVE", "Ce salon n'est pas actuellement actif").ToFailure<Unit>();

        _logger.LogInformation("Retour en mode standalone après désactivation du Salon {SalonId}", command.Id);
        var standaloneResult = await _mediator.Send(new ActivateStandaloneModeCommand(command.Origin), cancellationToken);
        if (standaloneResult.IsFail)
            return Error.Validation("STANDALONE_ACTIVATION_ERROR", "Impossible de revenir en mode standalone après désactivation du salon").ToFailure<Unit>();

        _logger.LogInformation("Salon {SalonId} désactivé, SVXLink en mode standalone", command.Id);
        return unit.ToSuccess();
    }
}
