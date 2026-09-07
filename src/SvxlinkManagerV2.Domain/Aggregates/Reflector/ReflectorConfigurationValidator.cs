using System.Text.RegularExpressions;

namespace SvxlinkManagerV2.Domain.Aggregates.Reflector;

/// <summary>Gravité d'une remarque sur la configuration du réflecteur.</summary>
public enum ReflectorConfigurationSeverity
{
    /// <summary>Le démon refusera de démarrer : l'enregistrement est bloqué.</summary>
    Error,

    /// <summary>Rien n'empêche le démarrage, mais la ligne est probablement une erreur.</summary>
    Warning
}

/// <summary>
/// Remarque sur une ligne de la configuration.
/// </summary>
/// <param name="Line">Numéro de ligne, à partir de 1.</param>
/// <param name="Severity">Gravité.</param>
/// <param name="Message">Ce qui ne va pas, en français.</param>
public record ReflectorConfigurationIssue(
    int Line,
    ReflectorConfigurationSeverity Severity,
    string Message);

/// <summary>
/// Vérifie la syntaxe INI de <c>svxreflector.conf</c> avant que le démon ne la refuse.
/// </summary>
/// <remarks>
/// <para>
/// <c>IniFile.ParseContent</c> est délibérément tolérant : il ignore en silence toute ligne
/// qu'il ne comprend pas. Le démon, lui, ne l'est pas — il répond
/// <c>Configuration file parse error. Illegal value syntax on line N</c> et refuse de
/// démarrer, en boucle. Enregistrer une configuration cassée met donc le réflecteur hors
/// service sans que rien ne l'ait annoncé.
/// </para>
/// <para>
/// Les sections inconnues ne sont qu'un avertissement : SVXLink en ajoute d'une version à
/// l'autre, et refuser ce que l'on ne connaît pas empêcherait d'utiliser une nouveauté de
/// l'amont.
/// </para>
/// </remarks>
public static class ReflectorConfigurationValidator
{
    /// <summary>Sections reconnues par svxreflector 25.05, hors talkgroups.</summary>
    private static readonly string[] KnownSections =
        ["GLOBAL", "ROOT_CA", "ISSUING_CA", "SERVER_CERT", "USERS", "PASSWORDS"];

    /// <summary>Section de talkgroup : <c>[TG#240]</c>.</summary>
    private static readonly Regex TalkGroupSection =
        new(@"^TG#\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Analyse la configuration et retourne ses défauts, dans l'ordre des lignes.
    /// </summary>
    /// <param name="config">Contenu INI brut.</param>
    public static IReadOnlyList<ReflectorConfigurationIssue> Validate(string? config)
    {
        var issues = new List<ReflectorConfigurationIssue>();

        if (string.IsNullOrWhiteSpace(config))
        {
            issues.Add(new(1, ReflectorConfigurationSeverity.Error,
                "La configuration est vide."));
            return issues;
        }

        var lines = config.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var currentSection = string.Empty;
        var hasGlobal = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var number = index + 1;
            var line = lines[index].Trim();

            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            if (line.StartsWith('['))
            {
                if (!line.EndsWith(']'))
                {
                    issues.Add(new(number, ReflectorConfigurationSeverity.Error,
                        $"Section non refermée : « {line} ». Il manque le crochet fermant."));
                    continue;
                }

                currentSection = line[1..^1].Trim();

                if (currentSection.Length == 0)
                {
                    issues.Add(new(number, ReflectorConfigurationSeverity.Error,
                        "Nom de section vide."));
                    continue;
                }

                if (currentSection.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase))
                    hasGlobal = true;
                else if (!IsKnownSection(currentSection))
                    issues.Add(new(number, ReflectorConfigurationSeverity.Warning,
                        $"Section inconnue : « {currentSection} ». Les talkgroups s'écrivent "
                        + "[TG#<numéro>]."));

                continue;
            }

            var separator = line.IndexOf('=');

            if (separator < 0)
            {
                issues.Add(new(number, ReflectorConfigurationSeverity.Error,
                    $"Ligne incomprise : « {Shorten(line)} ». Une ligne doit être un "
                    + "commentaire, une section ou une affectation clé=valeur."));
                continue;
            }

            if (separator == 0)
            {
                issues.Add(new(number, ReflectorConfigurationSeverity.Error,
                    "Nom de variable vide à gauche du signe égal."));
                continue;
            }

            if (currentSection.Length == 0)
                issues.Add(new(number, ReflectorConfigurationSeverity.Error,
                    $"« {line[..separator].Trim()} » est déclarée avant toute section : "
                    + "elle serait ignorée."));
        }

        if (!hasGlobal)
            issues.Add(new(1, ReflectorConfigurationSeverity.Error,
                "La configuration doit contenir une section [GLOBAL]."));

        return issues;
    }

    /// <summary>Indique si la configuration peut être enregistrée sans casser le démon.</summary>
    public static bool IsValid(string? config) =>
        !Validate(config).Any(issue => issue.Severity == ReflectorConfigurationSeverity.Error);

    private static bool IsKnownSection(string section) =>
        KnownSections.Contains(section, StringComparer.OrdinalIgnoreCase)
        || TalkGroupSection.IsMatch(section);

    /// <summary>Tronque une ligne trop longue pour un message d'erreur lisible.</summary>
    private static string Shorten(string line) =>
        line.Length <= 40 ? line : line[..40] + "…";
}
