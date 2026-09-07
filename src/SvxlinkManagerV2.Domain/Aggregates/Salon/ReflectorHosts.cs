using System.Text.RegularExpressions;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon;

/// <summary>
/// Liste de serveurs réflecteur additionnels, saisie sous forme <c>hôte[:port]</c> séparés
/// par des virgules.
/// </summary>
/// <remarks>
/// <para>
/// SVXLink 25.05 tente les entrées de <c>HOSTS</c> dans l'ordre : <c>HOST_PRIO</c> vaut 100
/// pour la première et <c>HOST_PRIO_INC</c> ajoute 1 à chaque suivante. <b>L'ordre de la
/// liste est donc la priorité</b>, et il n'y a rien de plus à exposer : demander à
/// l'opérateur de saisir des nombres de priorité reviendrait à lui faire réécrire ce que
/// SVXLink déduit déjà de l'ordre.
/// </para>
/// <para>
/// Un port omis prend celui de <c>HOST_PORT</c>, que la génération renseigne avec le port du
/// serveur principal.
/// </para>
/// </remarks>
public static class ReflectorHosts
{
    /// <summary>Un hôte, éventuellement suivi de son port.</summary>
    private static readonly Regex EntryPattern = new(
        @"^[A-Za-z0-9]([A-Za-z0-9.\-]*[A-Za-z0-9])?(:[0-9]{1,5})?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Domaine servant à la découverte par enregistrements SRV.</summary>
    private static readonly Regex DomainPattern = new(
        @"^[A-Za-z0-9]([A-Za-z0-9.\-]*[A-Za-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Découpe une saisie en entrées, en ignorant les blancs et les vides.</summary>
    public static IReadOnlyList<string> Parse(string? hosts) =>
        string.IsNullOrWhiteSpace(hosts)
            ? []
            : hosts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Toutes les entrées saisies sont-elles de la forme attendue ?</summary>
    public static bool IsValid(string? hosts) =>
        Parse(hosts).All(entry => EntryPattern.IsMatch(entry) && HasUsablePort(entry));

    /// <summary>Le domaine de découverte SRV est-il de la forme attendue ?</summary>
    public static bool IsValidDomain(string? domain) =>
        string.IsNullOrWhiteSpace(domain) || DomainPattern.IsMatch(domain.Trim());

    /// <summary>
    /// Construit la valeur de <c>HOSTS</c> : le serveur principal en tête, puis les
    /// additionnels dans l'ordre de saisie.
    /// </summary>
    /// <remarks>
    /// Les doublons du serveur principal sont écartés : le déclarer deux fois lui donnerait
    /// deux priorités et ferait retenter le même serveur en boucle.
    /// </remarks>
    public static string Build(string primaryHost, int primaryPort, string? additionalHosts)
    {
        var primary = $"{primaryHost}:{primaryPort}";

        var entries = new List<string> { primary };

        foreach (var entry in Parse(additionalHosts))
        {
            var normalized = entry.Contains(':') ? entry : $"{entry}:{primaryPort}";

            if (!entries.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                entries.Add(normalized);
        }

        return string.Join(",", entries);
    }

    private static bool HasUsablePort(string entry)
    {
        var separator = entry.IndexOf(':');
        if (separator < 0)
            return true;

        return int.TryParse(entry[(separator + 1)..], out var port) && port is > 0 and <= 65535;
    }
}
