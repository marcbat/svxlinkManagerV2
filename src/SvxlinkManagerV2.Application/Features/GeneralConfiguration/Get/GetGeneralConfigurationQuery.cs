using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;

namespace SvxlinkManagerV2.Application.Features.GeneralConfiguration.Get;

/// <summary>
/// Query pour récupérer la configuration générale.
/// </summary>
public record GetGeneralConfigurationQuery();

/// <summary>
/// Handler pour GetGeneralConfigurationQuery.
/// Retourne null si aucune configuration n'a encore été créée.
/// </summary>
public static class GetGeneralConfigurationQueryHandler
{
    public static async Task<GeneralConfigurationAggregate?> HandleAsync(
        GetGeneralConfigurationQuery query,
        IGeneralConfigurationRepository repository,
        CancellationToken ct = default)
    {
        return await repository.GetAsync(ct);
    }
}
