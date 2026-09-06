using System.Text;
using System.Text.RegularExpressions;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Network.Apt;

/// <summary>
/// Lecture et écriture du fichier de source APT qui porte le canal de mise à jour.
/// </summary>
public interface IAptSourceManager
{
    /// <summary>
    /// Canal actuellement déclaré dans le fichier de source, ou <c>null</c> si le
    /// fichier est absent ou illisible.
    /// </summary>
    ApplicationUpdateChannel? ReadChannel();

    /// <summary>Réécrit le fichier de source pour pointer sur le canal demandé.</summary>
    Validation<Error, Unit> WriteChannel(ApplicationUpdateChannel channel);

    /// <summary>Indique si le dépôt est configuré (source et trousseau présents).</summary>
    bool IsConfigured();
}

/// <inheritdoc cref="IAptSourceManager"/>
public class AptSourceManager : IAptSourceManager
{
    private readonly ILogger<AptSourceManager> _logger;
    private readonly AptUpdateOptions _options;

    public AptSourceManager(ILogger<AptSourceManager> logger, IOptions<AptUpdateOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Nom de la suite APT correspondant à un canal. Les suites du dépôt sont
    /// cumulatives : beta contient les versions stables, development contient tout.
    /// </summary>
    internal static string ToSuite(ApplicationUpdateChannel channel) => channel switch
    {
        ApplicationUpdateChannel.Stable => "stable",
        ApplicationUpdateChannel.Beta => "beta",
        ApplicationUpdateChannel.Development => "development",
        _ => "stable"
    };

    internal static ApplicationUpdateChannel? FromSuite(string suite) => suite.ToLowerInvariant() switch
    {
        "stable" => ApplicationUpdateChannel.Stable,
        "beta" => ApplicationUpdateChannel.Beta,
        "development" => ApplicationUpdateChannel.Development,
        _ => null
    };

    public bool IsConfigured() => File.Exists(_options.SourceListPath) && File.Exists(_options.KeyringPath);

    public ApplicationUpdateChannel? ReadChannel()
    {
        try
        {
            if (!File.Exists(_options.SourceListPath))
                return null;

            foreach (var line in File.ReadAllLines(_options.SourceListPath))
            {
                var channel = ParseChannel(line);
                if (channel is not null)
                    return channel;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lecture impossible du fichier de source APT {Path}", _options.SourceListPath);
            return null;
        }
    }

    /// <summary>
    /// Extrait la suite d'une ligne « deb [options] URL suite composant ».
    /// Le bloc d'options entre crochets est facultatif et retiré avant l'analyse.
    /// </summary>
    internal static ApplicationUpdateChannel? ParseChannel(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#') || !trimmed.StartsWith("deb", StringComparison.Ordinal))
            return null;

        var withoutOptions = Regex.Replace(trimmed, @"\[[^\]]*\]", " ");
        var parts = withoutOptions.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Attendu après nettoyage : deb <url> <suite> <composant…>
        if (parts.Length < 3 || !string.Equals(parts[0], "deb", StringComparison.Ordinal))
            return null;

        return FromSuite(parts[2]);
    }

    public Validation<Error, Unit> WriteChannel(ApplicationUpdateChannel channel)
    {
        var suite = ToSuite(channel);

        try
        {
            var directory = Path.GetDirectoryName(_options.SourceListPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var content = new StringBuilder()
                .AppendLine("# Fichier géré par SvxlinkManagerV2 — toute modification manuelle sera écrasée")
                .AppendLine("# lors du prochain changement de canal depuis l'interface.")
                .AppendLine(BuildSourceLine(suite))
                .ToString();

            File.WriteAllText(_options.SourceListPath, content);

            _logger.LogInformation(
                "Canal de mise à jour APT positionné sur « {Suite} » dans {Path}",
                suite,
                _options.SourceListPath);

            return Validation<Error, Unit>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Écriture impossible du fichier de source APT {Path}", _options.SourceListPath);
            return Error.Validation(
                    "APPLICATION_UPDATE_SOURCE_WRITE_FAILED",
                    $"Impossible d'écrire la source APT ({_options.SourceListPath}) : {ex.Message}")
                .ToFailure<Unit>();
        }
    }

    internal string BuildSourceLine(string suite) =>
        $"deb [arch={_options.Architecture} signed-by={_options.KeyringPath}] " +
        $"{_options.RepositoryUrl.TrimEnd('/')} {suite} {_options.Component}";
}
