using MediatR;

namespace SvxlinkManagerV2.Application.Features.Ping;

/// <summary>
/// Commande de test pour valider le fonctionnement du mécanisme CQRS avec MediatR.
/// Convention : La Command et son Handler sont définis dans le même fichier pour améliorer la lisibilité.
/// </summary>
/// <param name="Message">Message à envoyer avec la commande Ping</param>
public record PingCommand(string Message) : IRequest<string>;

/// <summary>
/// Handler pour la commande PingCommand.
/// </summary>
public class PingCommandHandler : IRequestHandler<PingCommand, string>
{
    public Task<string> Handle(PingCommand command, CancellationToken cancellationToken)
        => Task.FromResult($"Pong: {command.Message}");
}
