using System.Text;
using System.Text.RegularExpressions;

namespace SvxlinkManagerV2.Application.Features.Diagnostics;

/// <summary>
/// Expurge les secrets d'un contenu textuel destiné à l'archive de diagnostic.
/// L'archive étant transmise à un tiers pour analyse, aucune clé d'authentification
/// réflecteur ni mot de passe ne doit y figurer.
/// </summary>
public static class DiagnosticSecretRedactor
{
    /// <summary>
    /// Valeur substituée à celle d'une affectation reconnue comme secrète.
    /// </summary>
    public const string RedactedValue = "***EXPURGE***";

    /// <summary>
    /// Sections INI dont **toutes** les valeurs sont considérées comme secrètes
    /// (ex. la section [PASSWORDS] du svxreflector, qui associe un indicatif à son mot de passe).
    /// </summary>
    private static readonly string[] SecretSections = ["PASSWORDS"];

    /// <summary>
    /// Fragments de nom de clé déclenchant l'expurgation de la valeur associée.
    /// </summary>
    private static readonly string[] SecretKeyFragments =
    [
        "AUTH_KEY",
        "PASSWORD",
        "PASSWD",
        "SECRET",
        "TOKEN",
        "PSK",
        "PRIVATE_KEY"
    ];

    /// <summary>
    /// Affectation « CLE = valeur » : la clé est capturée pour être confrontée aux fragments secrets.
    /// </summary>
    private static readonly Regex AssignmentPattern = new(
        @"(?<key>[A-Za-z0-9_\-\.]+)(?<separator>\s*=\s*)(?<value>.*)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Déclaration de section INI, ex. « [ReflectorLogic] ».
    /// </summary>
    private static readonly Regex SectionPattern = new(
        @"^\s*\[(?<name>[^\]]+)\]\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Expurge les secrets d'un contenu textuel : fichier de configuration INI ou lignes de log.
    /// Le suivi des sections permet d'expurger l'intégralité des sections listées dans
    /// <see cref="SecretSections"/> ; ailleurs, seules les clés reconnues comme secrètes le sont.
    /// </summary>
    /// <param name="content">Contenu à expurger.</param>
    /// <returns>Le contenu, valeurs secrètes remplacées par <see cref="RedactedValue"/>.</returns>
    public static string Redact(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return content ?? string.Empty;

        var builder = new StringBuilder(content.Length);
        var currentSection = string.Empty;
        var isFirstLine = true;

        foreach (var line in content.Split('\n'))
        {
            if (!isFirstLine)
                builder.Append('\n');

            isFirstLine = false;

            // Le découpage sur '\n' laisse un '\r' final sur les fichiers CRLF : il est
            // réattaché tel quel pour ne pas altérer les fins de ligne du fichier d'origine.
            var carriageReturn = line.EndsWith('\r') ? "\r" : string.Empty;
            var rawLine = carriageReturn.Length == 0 ? line : line[..^1];

            var section = SectionPattern.Match(rawLine);
            if (section.Success)
                currentSection = section.Groups["name"].Value;

            builder.Append(RedactLine(rawLine, currentSection)).Append(carriageReturn);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Indique qu'une clé porte un secret, d'après son nom ou la section qui la contient.
    /// </summary>
    private static bool IsSecret(string key, string section)
        => SecretSections.Any(s => string.Equals(s, section, StringComparison.OrdinalIgnoreCase))
           || SecretKeyFragments.Any(f => key.Contains(f, StringComparison.OrdinalIgnoreCase));

    private static string RedactLine(string line, string section)
    {
        var match = AssignmentPattern.Match(line);
        if (!match.Success)
            return line;

        if (!IsSecret(match.Groups["key"].Value, section))
            return line;

        var prefix = line[..match.Groups["key"].Index];

        return $"{prefix}{match.Groups["key"].Value}{match.Groups["separator"].Value}{RedactedValue}";
    }
}
