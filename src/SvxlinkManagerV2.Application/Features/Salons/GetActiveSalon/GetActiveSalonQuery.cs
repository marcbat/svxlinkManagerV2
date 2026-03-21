using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;

/// <summary>
/// Query pour récupérer le Salon actuellement actif
/// </summary>
public record GetActiveSalonQuery();

/// <summary>
/// Handler pour la query GetActiveSalonQuery
/// </summary>
public static class GetActiveSalonQueryHandler
{
    /// <summary>
    /// Récupère le Salon actif en utilisant le tracker d'état runtime.
    /// Retourne null si aucun salon n'est actif.
    /// </summary>
    public static async Task<SalonAggregate?> Handle(
        GetActiveSalonQuery query,
        ISalonRepository repository,
        IActiveSessionTracker tracker,
        CancellationToken cancellationToken)
    {
        var activeSalonId = tracker.ActiveSalonId;
        if (!activeSalonId.HasValue)
            return null;

        var result = await repository.GetByIdAsync(activeSalonId.Value, cancellationToken);
        return result.Match(
            Succ: a => a.IsDeleted ? null : a,
            Fail: _ => null);
    }
}
