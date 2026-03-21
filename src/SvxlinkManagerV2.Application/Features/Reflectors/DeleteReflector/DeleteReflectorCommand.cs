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
    public static async Task<Validation<Error, Unit>> Handle(
        DeleteReflectorCommand command,
        IReflectorRepository repository,
        IActiveSessionTracker tracker,
        CancellationToken cancellationToken)
    {
        if (tracker.IsReflectorActive(command.Id))
            return Error.Validation("REFLECTOR_ACTIVE", "Impossible de supprimer un reflector actif").ToFailure<Unit>();

        return await repository.DeleteAsync(command.Id, cancellationToken);
    }
}
