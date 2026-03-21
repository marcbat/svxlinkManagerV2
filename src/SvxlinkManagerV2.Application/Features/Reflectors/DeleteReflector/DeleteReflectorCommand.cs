using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Reflectors.DeleteReflector;

/// <summary>
/// Commande pour supprimer (soft delete) un Reflector.
/// Bloquée si le reflector est actif.
/// </summary>
/// <param name="Id">Identifiant du reflector à supprimer</param>
public record DeleteReflectorCommand(Guid Id);

/// <summary>
/// Handler pour la commande DeleteReflectorCommand
/// </summary>
public static class DeleteReflectorCommandHandler
{
    /// <summary>
    /// Effectue la suppression logique du Reflector
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        DeleteReflectorCommand command,
        IReflectorRepository repository,
        CancellationToken cancellationToken)
    {
        return await repository.DeleteAsync(command.Id, cancellationToken);
    }
}
