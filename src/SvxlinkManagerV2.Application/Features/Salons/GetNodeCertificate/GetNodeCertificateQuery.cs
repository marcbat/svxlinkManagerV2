using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Application.Features.Salons.GetNodeCertificate;

/// <summary>
/// État du certificat X.509 du nœud pour le salon actif.
/// </summary>
public record GetNodeCertificateQuery() : IRequest<NodeCertificateState>;

/// <summary>
/// Handler de <see cref="GetNodeCertificateQuery"/>.
/// </summary>
/// <remarks>
/// L'indicatif vient du salon actif : c'est lui qui nomme les fichiers de la PKI, et il
/// devient le Common Name du certificat. Hors V3 la question ne se pose pas.
/// </remarks>
public class GetNodeCertificateQueryHandler : IRequestHandler<GetNodeCertificateQuery, NodeCertificateState>
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;
    private readonly INodeCertificateReader _reader;

    public GetNodeCertificateQueryHandler(
        ISalonRepository repository,
        IActiveSessionTracker tracker,
        INodeCertificateReader reader)
    {
        _repository = repository;
        _tracker = tracker;
        _reader = reader;
    }

    public async Task<NodeCertificateState> Handle(
        GetNodeCertificateQuery query,
        CancellationToken cancellationToken)
    {
        var activeSalonId = _tracker.ActiveSalonId;
        if (!activeSalonId.HasValue)
            return NodeCertificateState.NotApplicable;

        var result = await _repository.GetByIdAsync(activeSalonId.Value, cancellationToken);
        if (result.IsFail)
            return NodeCertificateState.NotApplicable;

        var salon = result.Match(
            Succ: aggregate => aggregate,
            Fail: _ => throw new InvalidOperationException());

        if (salon.SalonType == SalonType.Parrot ||
            salon.Configuration.ReflectorProtocol != ReflectorProtocol.V3)
            return NodeCertificateState.NotApplicable;

        return _reader.Read(salon.Configuration.Callsign);
    }
}
