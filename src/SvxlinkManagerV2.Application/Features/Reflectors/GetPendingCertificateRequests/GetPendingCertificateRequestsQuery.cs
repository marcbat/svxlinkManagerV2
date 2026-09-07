using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using LangExtError = LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Application.Features.Reflectors.GetPendingCertificateRequests;

/// <summary>
/// Demandes de signature de certificat en attente sur le réflecteur local.
/// </summary>
public record GetPendingCertificateRequestsQuery()
    : IRequest<Validation<LangExtError, IReadOnlyList<PendingCertificateRequest>>>;

/// <summary>
/// Handler de <see cref="GetPendingCertificateRequestsQuery"/>.
/// </summary>
public class GetPendingCertificateRequestsQueryHandler
    : IRequestHandler<GetPendingCertificateRequestsQuery,
        Validation<LangExtError, IReadOnlyList<PendingCertificateRequest>>>
{
    private readonly IPendingCertificateRequestReader _reader;

    public GetPendingCertificateRequestsQueryHandler(IPendingCertificateRequestReader reader)
    {
        _reader = reader;
    }

    public Task<Validation<LangExtError, IReadOnlyList<PendingCertificateRequest>>> Handle(
        GetPendingCertificateRequestsQuery query,
        CancellationToken cancellationToken) =>
        _reader.ListAsync(cancellationToken);
}
