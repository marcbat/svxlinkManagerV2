using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Application.Features.Reflectors.GetReflectorStatus;

/// <summary>
/// Dernier statut connu du réflecteur local.
/// </summary>
/// <remarks>
/// La lecture est servie depuis l'instantané tenu par <see cref="IReflectorStatusService"/> :
/// la page n'interroge jamais le serveur HTTP elle-même. Un onglet ouvert ne doit pas
/// multiplier les requêtes vers un serveur que sa propre documentation décrit comme
/// sensible à la charge.
/// </remarks>
public record GetReflectorStatusQuery() : IRequest<ReflectorStatusSnapshot>;

/// <summary>
/// Handler de <see cref="GetReflectorStatusQuery"/>.
/// </summary>
public class GetReflectorStatusQueryHandler : IRequestHandler<GetReflectorStatusQuery, ReflectorStatusSnapshot>
{
    private readonly IReflectorStatusService _statusService;

    public GetReflectorStatusQueryHandler(IReflectorStatusService statusService)
    {
        _statusService = statusService;
    }

    public Task<ReflectorStatusSnapshot> Handle(
        GetReflectorStatusQuery query,
        CancellationToken cancellationToken) =>
        Task.FromResult(_statusService.Current);
}
