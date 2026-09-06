using System.Reflection;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Network.Apt;

/// <summary>
/// Consultation des mises à jour via le dépôt APT du projet.
/// Remplace l'interrogation de l'API GitHub Releases : le dépôt est public et signé,
/// et apt compare les versions selon les règles Debian — « 1.5.0~alpha.10 » se classe
/// bien avant « 1.5.0 », ce que la comparaison maison devait reproduire à la main.
/// </summary>
public class AptApplicationUpdateService : IApplicationUpdateService
{
    private readonly IAptCommandRunner _runner;
    private readonly IAptSourceManager _sourceManager;
    private readonly ILogger<AptApplicationUpdateService> _logger;
    private readonly AptUpdateOptions _options;

    public AptApplicationUpdateService(
        IAptCommandRunner runner,
        IAptSourceManager sourceManager,
        ILogger<AptApplicationUpdateService> logger,
        IOptions<AptUpdateOptions> options)
    {
        _runner = runner;
        _sourceManager = sourceManager;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Validation<Error, ApplicationUpdateStatusDto>> GetStatusAsync(
        ApplicationUpdateChannel? channel = null,
        CancellationToken cancellationToken = default)
    {
        var currentVersion = GetAssemblyVersion();

        if (!_options.Enabled)
        {
            return Success(currentVersion, channel ?? ResolveDefaultChannel(), false, null,
                "La vérification des mises à jour est désactivée.");
        }

        // Le canal demandé fait autorité : le sélectionner dans l'interface réécrit la
        // source APT, de sorte que ce qui est affiché est bien ce qui est configuré.
        var effectiveChannel = channel ?? _sourceManager.ReadChannel() ?? ResolveDefaultChannel();

        if (_sourceManager.ReadChannel() != effectiveChannel)
        {
            var write = _sourceManager.WriteChannel(effectiveChannel);
            if (write.IsFail)
                return write.Map(_ => default(ApplicationUpdateStatusDto)!);
        }

        var refresh = await RefreshIndexAsync(cancellationToken);
        if (refresh.IsFail)
            return refresh.Map(_ => default(ApplicationUpdateStatusDto)!);

        var policy = await _runner.RunAsync("apt-cache", ["policy", _options.PackageName], cancellationToken);
        if (!policy.Succeeded)
        {
            return Error.Validation(
                    "APPLICATION_UPDATE_APT_CACHE_FAILED",
                    $"Impossible de consulter le paquet {_options.PackageName} : {policy.ErrorMessage}")
                .ToFailure<ApplicationUpdateStatusDto>();
        }

        var (installed, candidate) = ParsePolicy(policy.StandardOutput);

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return Success(installed ?? currentVersion, effectiveChannel, true, null,
                "Aucune version n'est proposée par le dépôt sur ce canal.");
        }

        // La version rapportée par dpkg fait foi ; celle de l'assembly ne sert que
        // lorsque l'application ne tourne pas depuis un paquet installé (poste de dev).
        var installedVersion = installed ?? currentVersion;
        var updateAvailable = !string.Equals(installedVersion, candidate, StringComparison.OrdinalIgnoreCase)
            && await IsUpgradeAsync(installedVersion, candidate, cancellationToken);

        var release = new ApplicationReleaseInfo(
            Version: candidate,
            Tag: ToReleaseTag(candidate),
            Name: $"{_options.PackageName} {candidate}",
            PublishedAt: DateTimeOffset.MinValue,
            IsPrerelease: candidate.Contains('~'),
            ReleaseNotesUrl: BuildReleaseNotesUrl(candidate),
            ChecksumUrl: null,
            // Le dépôt ne publie pas d'URL directe : apt résout et télécharge lui-même.
            PackageUrl: null,
            PackageName: _options.PackageName);

        return Success(
            installedVersion,
            effectiveChannel,
            true,
            updateAvailable ? release : null,
            updateAvailable
                ? "Une nouvelle version est disponible sur le canal sélectionné."
                : "Vous disposez déjà de la dernière version connue pour ce canal.",
            updateAvailable);
    }

    /// <summary>
    /// Rafraîchit l'index du seul dépôt du projet. Recharger toutes les sources de la
    /// machine prendrait plusieurs dizaines de secondes sur un Orange Pi et ferait
    /// dépendre la page d'un miroir Debian éventuellement injoignable.
    /// </summary>
    internal async Task<Validation<Error, Unit>> RefreshIndexAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            "apt-get",
            [
                "update",
                "-o", $"Dir::Etc::sourcelist={_options.SourceListPath}",
                "-o", "Dir::Etc::sourceparts=/dev/null",
                "-o", "APT::Get::List-Cleanup=0"
            ],
            cancellationToken);

        if (result.Succeeded)
            return Validation<Error, Unit>.Success(Unit.Default);

        _logger.LogWarning("Échec du rafraîchissement de l'index APT : {Error}", result.ErrorMessage);

        return Error.Validation(
                "APPLICATION_UPDATE_APT_REFRESH_FAILED",
                $"Impossible de rafraîchir l'index du dépôt : {result.ErrorMessage}")
            .ToFailure<Unit>();
    }

    /// <summary>
    /// Compare deux versions avec dpkg, seul juge fiable de l'ordre Debian
    /// (« 1.5.0~alpha.10 » précède « 1.5.0 », qui précède « 1.5.0.1 »).
    /// </summary>
    internal async Task<bool> IsUpgradeAsync(string installed, string candidate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(installed))
            return true;

        var result = await _runner.RunAsync(
            "dpkg",
            ["--compare-versions", candidate, "gt", installed],
            cancellationToken);

        // dpkg --compare-versions ne produit aucune sortie : seul le code compte.
        return result.ExitCode == 0;
    }

    /// <summary>
    /// Extrait les versions installée et candidate de la sortie d'apt-cache policy.
    /// La locale neutre imposée par le runner garantit les libellés anglais.
    /// </summary>
    internal static (string? Installed, string? Candidate) ParsePolicy(string output)
    {
        string? installed = null;
        string? candidate = null;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();

            if (line.StartsWith("Installed:", StringComparison.OrdinalIgnoreCase))
                installed = Normalize(line["Installed:".Length..]);
            else if (line.StartsWith("Candidate:", StringComparison.OrdinalIgnoreCase))
                candidate = Normalize(line["Candidate:".Length..]);
        }

        return (installed, candidate);

        static string? Normalize(string value)
        {
            var trimmed = value.Trim();
            return trimmed.Length == 0 || trimmed == "(none)" ? null : trimmed;
        }
    }

    /// <summary>
    /// Reconstruit le tag GitHub d'une version Debian : le tilde des préversions
    /// Debian correspond au tiret de la version sémantique (1.5.0~alpha.8 → v1.5.0-alpha.8).
    /// </summary>
    internal static string ToReleaseTag(string debianVersion) => $"v{debianVersion.Replace('~', '-')}";

    private string? BuildReleaseNotesUrl(string debianVersion) =>
        string.IsNullOrWhiteSpace(_options.ReleaseNotesRepository)
            ? null
            : $"https://github.com/{_options.ReleaseNotesRepository}/releases/tag/{ToReleaseTag(debianVersion)}";

    private ApplicationUpdateChannel ResolveDefaultChannel() =>
        Enum.TryParse<ApplicationUpdateChannel>(_options.DefaultChannel, ignoreCase: true, out var parsed)
            ? parsed
            : ApplicationUpdateChannel.Stable;

    internal static string GetAssemblyVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
            return assembly.GetName().Version?.ToString() ?? "0.0.0";

        // Le suffixe de métadonnées de build (« +sha ») n'a pas d'équivalent Debian.
        var plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }

    private static Validation<Error, ApplicationUpdateStatusDto> Success(
        string currentVersion,
        ApplicationUpdateChannel channel,
        bool isConfigured,
        ApplicationReleaseInfo? release,
        string message,
        bool updateAvailable = false)
        => Validation<Error, ApplicationUpdateStatusDto>.Success(
            new ApplicationUpdateStatusDto(
                CurrentVersion: currentVersion,
                Channel: channel,
                IsConfigured: isConfigured,
                IsUpdateAvailable: updateAvailable,
                LatestRelease: release,
                Message: message));
}
