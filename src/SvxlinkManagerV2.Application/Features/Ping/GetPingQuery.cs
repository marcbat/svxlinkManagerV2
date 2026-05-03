using MediatR;

namespace SvxlinkManagerV2.Application.Features.Ping;

/// <summary>
/// Query de test pour vérifier l'état du service Ping.
/// Convention : La Query et son Handler sont définis dans le même fichier pour améliorer la lisibilité.
/// </summary>
public record GetPingQuery() : IRequest<string>;

/// <summary>
/// Handler pour la query GetPingQuery.
/// </summary>
public class GetPingQueryHandler : IRequestHandler<GetPingQuery, string>
{
    public Task<string> Handle(GetPingQuery query, CancellationToken cancellationToken)
        => Task.FromResult("Ping service is alive");
}
