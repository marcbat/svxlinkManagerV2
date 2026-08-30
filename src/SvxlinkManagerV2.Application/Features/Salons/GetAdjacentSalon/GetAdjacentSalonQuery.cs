using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Features.Salons.GetAdjacentSalon;

/// <summary>
/// Sens de navigation dans la liste des salons ordonnée par code DTMF.
/// </summary>
public enum SalonNavigationDirection
{
    /// <summary>Salon suivant.</summary>
    Next,

    /// <summary>Salon précédent.</summary>
    Previous
}

/// <summary>
/// Query de navigation entre salons (commandes DTMF 312 et 313).
/// Parcourt en boucle les salons non supprimés dotés d'un code DTMF, ordonnés par code,
/// en partant du salon actuellement actif.
/// </summary>
/// <param name="Direction">Sens de navigation.</param>
public record GetAdjacentSalonQuery(SalonNavigationDirection Direction) : IRequest<SalonAggregate?>;

/// <summary>
/// Handler pour la query GetAdjacentSalonQuery.
/// Retourne null si aucun salon n'est doté d'un code DTMF.
/// Si aucun salon n'est actif (ou si l'actif n'a pas de code DTMF), retourne
/// le premier salon de la liste en navigation avant, le dernier en navigation arrière.
/// </summary>
public class GetAdjacentSalonQueryHandler : IRequestHandler<GetAdjacentSalonQuery, SalonAggregate?>
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;
    private readonly ILogger<GetAdjacentSalonQueryHandler> _logger;

    public GetAdjacentSalonQueryHandler(
        ISalonRepository repository,
        IActiveSessionTracker tracker,
        ILogger<GetAdjacentSalonQueryHandler> logger)
    {
        _repository = repository;
        _tracker = tracker;
        _logger = logger;
    }

    public async Task<SalonAggregate?> Handle(
        GetAdjacentSalonQuery query,
        CancellationToken cancellationToken)
    {
        var salons = await _repository.GetAllAsync(cancellationToken);

        // GetAllAsync exclut déjà les salons supprimés — on ne retient que ceux
        // joignables par radio, c'est-à-dire dotés d'un code DTMF.
        var navigable = salons
            .Where(s => !s.IsDeleted && s.DtmfCode.HasValue)
            .OrderBy(s => s.DtmfCode!.Value)
            .ToList();

        if (navigable.Count == 0)
        {
            _logger.LogDebug("Navigation entre salons impossible : aucun salon doté d'un code DTMF");
            return null;
        }

        var step = query.Direction == SalonNavigationDirection.Next ? 1 : -1;

        var activeSalonId = _tracker.ActiveSalonId;
        var currentIndex = activeSalonId.HasValue
            ? navigable.FindIndex(s => s.Id == activeSalonId.Value)
            : -1;

        if (currentIndex < 0)
        {
            // Aucun point de départ : on entre dans la liste par une extrémité.
            return step == 1 ? navigable[0] : navigable[^1];
        }

        var nextIndex = (currentIndex + step + navigable.Count) % navigable.Count;
        return navigable[nextIndex];
    }
}
