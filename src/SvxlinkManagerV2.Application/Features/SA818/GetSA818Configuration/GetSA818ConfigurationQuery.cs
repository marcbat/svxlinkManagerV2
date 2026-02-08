using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.SA818.GetSA818Configuration;

/// <summary>
/// Query pour récupérer la configuration actuelle du module SA818.
/// Utilise la projection Marten pour des performances optimales.
/// </summary>
public record GetSA818ConfigurationQuery();

/// <summary>
/// Handler pour la query GetSA818ConfigurationQuery.
/// Retourne la configuration SA818 depuis la base de données.
/// </summary>
public static class GetSA818ConfigurationQueryHandler
{
    /// <summary>
    /// Traite la query de récupération de la configuration SA818.
    /// Retourne une Validation contenant le DTO de configuration ou une erreur si le SA818 n'est pas initialisé.
    /// </summary>
    public static async Task<Validation<Error, SA818ConfigurationDto>> Handle(
        GetSA818ConfigurationQuery query,
        ISA818Repository repository,
        CancellationToken cancellationToken)
    {
        var configuration = await repository.GetConfigurationAsync(cancellationToken);

        if (configuration == null)
        {
            return Error.NotFound("SA818", "Configuration SA818 non initialisée")
                .ToFailure<SA818ConfigurationDto>();
        }

        return configuration.ToSuccess();
    }
}
