using LanguageExt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Lecture du fichier svxlink.conf déployé sur la machine.
/// Le chemin provient de la clé de configuration <c>SvxLink:ConfigPath</c>, la même que
/// celle utilisée par le daemon et les diagnostics de démarrage.
/// </summary>
public class SvxLinkConfigurationReader : ISvxLinkConfigurationReader
{
    private const string DefaultConfigPath = "/etc/svxlink/svxlink.conf";

    private readonly ILogger<SvxLinkConfigurationReader> _logger;

    public SvxLinkConfigurationReader(
        IConfiguration configuration,
        ILogger<SvxLinkConfigurationReader> logger)
    {
        _logger = logger;
        ConfigurationPath = configuration["SvxLink:ConfigPath"] ?? DefaultConfigPath;
    }

    /// <inheritdoc />
    public string ConfigurationPath { get; }

    /// <inheritdoc />
    public async Task<Validation<Error, string>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigurationPath))
        {
            _logger.LogInformation(
                "Fichier de configuration SVXLink absent : {Path} — aucun salon n'a encore été activé",
                ConfigurationPath);

            return Error
                .Validation("SVXLINK_CONFIG_NOT_FOUND", $"Fichier de configuration introuvable : {ConfigurationPath}")
                .ToFailure<string>();
        }

        try
        {
            return await File.ReadAllTextAsync(ConfigurationPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lecture impossible du fichier {Path}", ConfigurationPath);

            return Error
                .Validation("SVXLINK_CONFIG_READ_ERROR", $"Lecture impossible du fichier : {ex.Message}")
                .ToFailure<string>();
        }
    }
}
