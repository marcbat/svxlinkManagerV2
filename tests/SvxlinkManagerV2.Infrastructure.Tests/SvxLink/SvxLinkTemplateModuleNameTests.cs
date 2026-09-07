using FluentAssertions;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Garde sur les templates <c>svxlink.conf</c> versionnés : le nom de bibliothèque déduit
/// d'une section de module doit désigner un module que SVXLink sait charger.
/// </summary>
/// <remarks>
/// SVXLink construit le nom du fichier chargé par <c>"Module" + &lt;nom&gt; + ".so"</c>, où
/// &lt;nom&gt; vaut le nom de la section listée dans <c>MODULES</c>, écrasé par <c>NAME</c>
/// puis par <c>PLUGIN_NAME</c> quand ils sont présents (<c>Logic::loadModule</c>, à
/// l'identique en 19.09.2 et en 25.05). Écrire <c>NAME=ModuleHelp</c> réclamait donc
/// <c>ModuleModuleHelp.so</c>, et le module d'aide n'était jamais chargé — ticket #109.
///
/// Le test vise la <b>classe</b> d'erreur, pas l'occurrence corrigée : toute section de
/// module ajoutée plus tard est contrôlée sans rien écrire de plus.
///
/// Il lit le texte brut plutôt que de passer par <c>IniFile</c> : ce parser est
/// volontairement tolérant et ignore en silence ce qu'il ne comprend pas, ce qui est
/// exactement la mauvaise propriété pour une garde.
/// </remarks>
public class SvxLinkTemplateModuleNameTests
{
    /// <summary>Les deux templates versionnés : cible de production, et stack Docker.</summary>
    public static TheoryData<string> Templates =>
    [
        "svxlink-config",
        "svxlink-config-docker"
    ];

    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_ShouldNotRepeatTheModulePrefixInPluginNames(string configDirectory)
    {
        var offenders = ResolveModulePluginNames(configDirectory)
            .Where(module => module.Value.StartsWith("Module", StringComparison.Ordinal))
            .Select(module => $"[{module.Key}] réclame Module{module.Value}.so")
            .ToList();

        offenders.Should().BeEmpty(
            "SVXLink préfixe déjà « Module » : un nom qui le répète désigne une bibliothèque inexistante");
    }

    /// <summary>
    /// Les deux modules déclarés doivent viser les bibliothèques réellement livrées par
    /// SVXLink, <c>ModuleHelp.so</c> et <c>ModuleParrot.so</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_ShouldTargetTheModulesShippedBySvxLink(string configDirectory)
    {
        var modules = ResolveModulePluginNames(configDirectory);

        modules.Should().ContainKey("ModuleHelp").WhoseValue.Should().Be("Help");
        modules.Should().ContainKey("ModuleParrot").WhoseValue.Should().Be("Parrot");
    }

    /// <summary>
    /// Nom de bibliothèque résolu pour chaque section <c>[Module*]</c> du template, en
    /// appliquant les règles de <c>Logic::loadModule</c> : le nom de la section, puis
    /// <c>NAME</c>, puis <c>PLUGIN_NAME</c>.
    /// </summary>
    private static Dictionary<string, string> ResolveModulePluginNames(string configDirectory)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        string? section = null;

        foreach (var rawLine in File.ReadLines(FindTemplate(configDirectory)))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();

                // Sans NAME ni PLUGIN_NAME, c'est le nom de la section qui fait foi.
                if (section.StartsWith("Module", StringComparison.Ordinal))
                    resolved[section] = section;

                continue;
            }

            if (section is null || !resolved.ContainsKey(section))
                continue;

            var separator = line.IndexOf('=');
            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            if (key is "NAME" or "PLUGIN_NAME")
                resolved[section] = line[(separator + 1)..].Trim();
        }

        return resolved;
    }

    /// <summary>
    /// Remonte l'arborescence depuis le répertoire d'exécution jusqu'au template versionné,
    /// comme le fait déjà <c>SvxLinkConfigurationServiceTests</c>.
    /// </summary>
    private static string FindTemplate(string configDirectory)
    {
        var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, configDirectory, "svxlink.conf");
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException($"Template introuvable : {configDirectory}/svxlink.conf");
    }
}
