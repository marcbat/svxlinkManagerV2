using System.Text;

namespace SvxlinkManagerV2.Domain.Aggregates.Reflector;

/// <summary>
/// Ajoute des réglages manquants à une configuration existante, sans rien y détruire.
/// </summary>
/// <remarks>
/// <para>
/// La fusion est <b>textuelle</b>, et c'est délibéré. Passer par <c>IniFile</c> serait plus
/// court mais son analyseur ne conserve pas les commentaires des sections : réécrire le
/// fichier à partir de lui effacerait toutes les notes de l'opérateur. Or c'est exactement
/// ce qu'il s'agit de ne pas faire.
/// </para>
/// <para>
/// Rien n'est jamais modifié ni supprimé : seules des lignes manquantes sont ajoutées, à la
/// fin de leur section, précédées de leur justification en commentaire.
/// </para>
/// </remarks>
public static class ReflectorConfigurationMerger
{
    /// <summary>
    /// Retourne la configuration enrichie des réglages donnés.
    /// </summary>
    /// <param name="config">Configuration existante, préservée telle quelle.</param>
    /// <param name="settings">Réglages à ajouter. Ceux déjà déclarés sont ignorés.</param>
    public static string Add(string config, IReadOnlyList<ReflectorRecommendedSetting> settings)
    {
        var toAdd = settings
            .Where(setting => ReflectorRecommendedSettings.MissingFrom(config).Contains(setting))
            .ToList();

        if (toAdd.Count == 0)
            return config;

        var lines = config.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();

        // Les sections sont traitées une par une : insérer dans l'une décale les suivantes,
        // et regrouper les ajouts évite d'avoir à recalculer les positions à chaque clé.
        foreach (var group in toAdd.GroupBy(setting => setting.Section, StringComparer.OrdinalIgnoreCase))
            InsertIntoSection(lines, group.Key, group.ToList());

        return string.Join("\n", lines);
    }

    private static void InsertIntoSection(
        List<string> lines,
        string section,
        IReadOnlyList<ReflectorRecommendedSetting> settings)
    {
        var block = BuildBlock(settings);
        var insertAt = FindEndOfSection(lines, section);

        if (insertAt is null)
        {
            // Section absente : elle est créée à la fin, ce qui ne perturbe aucune des
            // sections existantes.
            lines.Add(string.Empty);
            lines.Add($"[{section}]");
            lines.AddRange(block);
            return;
        }

        lines.InsertRange(insertAt.Value, block);
    }

    private static IReadOnlyList<string> BuildBlock(IReadOnlyList<ReflectorRecommendedSetting> settings)
    {
        var block = new List<string> { string.Empty };

        foreach (var setting in settings)
        {
            foreach (var line in Wrap(setting.Rationale))
                block.Add($"# {line}");

            block.Add($"{setting.Key}={setting.Value}");
            block.Add(string.Empty);
        }

        // La ligne vide finale ferait doublon avec celle qui suit le point d'insertion.
        block.RemoveAt(block.Count - 1);
        return block;
    }

    /// <summary>
    /// Indice de la ligne qui suit la dernière ligne utile de la section, ou <c>null</c> si
    /// la section n'existe pas.
    /// </summary>
    /// <remarks>
    /// Les lignes vides de fin de section sont exclues : insérer avant elles garde la
    /// respiration entre sections.
    /// </remarks>
    private static int? FindEndOfSection(List<string> lines, string section)
    {
        var header = $"[{section}]";
        var start = lines.FindIndex(line => line.Trim().Equals(header, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
            return null;

        var end = lines.FindIndex(start + 1, line => line.Trim().StartsWith('['));
        if (end < 0)
            end = lines.Count;

        while (end > start + 1 && string.IsNullOrWhiteSpace(lines[end - 1]))
            end--;

        return end;
    }

    /// <summary>Découpe une justification en lignes de commentaire lisibles.</summary>
    private static IReadOnlyList<string> Wrap(string text, int width = 76)
    {
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > width)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
                current.Append(' ');

            current.Append(word);
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        return lines;
    }
}
