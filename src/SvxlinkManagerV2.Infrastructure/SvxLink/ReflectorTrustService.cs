using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Implémentation de <see cref="IReflectorTrustService"/> : supprime le bundle CA
/// mémorisé par le nœud.
/// </summary>
/// <remarks>
/// Rien d'autre n'est touché dans <c>CERT_PKI_DIR</c>. La clé privée et le certificat signé
/// du nœud y vivent aussi, et les effacer imposerait au réflecteur de signer une nouvelle
/// CSR — une manœuvre bien plus lourde que le problème à résoudre.
/// </remarks>
public class ReflectorTrustService : IReflectorTrustService
{
    private readonly ILogger<ReflectorTrustService> _logger;
    private readonly string _caBundlePath;

    public ReflectorTrustService(ILogger<ReflectorTrustService> logger, string? caBundlePath = null)
    {
        _logger = logger;
        _caBundlePath = caBundlePath ?? SvxLinkPkiPaths.CaBundleFile;
    }

    /// <inheritdoc/>
    public Task<Validation<Error, bool>> ResetTrustAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Path.Exists et non File.Exists : si un répertoire occupe la place du bundle,
            // le nœud est dans un état cassé qu'il vaut mieux signaler que traiter comme
            // « rien à supprimer » — File.Delete lèvera, et l'échec remontera.
            if (!Path.Exists(_caBundlePath))
            {
                _logger.LogInformation(
                    "Aucun bundle CA à supprimer ({Path}) : le nœud en retéléchargera un au prochain contact",
                    _caBundlePath);
                return Task.FromResult<Validation<Error, bool>>(false);
            }

            File.Delete(_caBundlePath);
            _logger.LogWarning("Bundle CA supprimé : {Path}", _caBundlePath);

            return Task.FromResult<Validation<Error, bool>>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la suppression du bundle CA {Path}", _caBundlePath);
            return Task.FromResult(Validation<Error, bool>.Fail(Seq1(Error.New(ex))));
        }
    }
}
