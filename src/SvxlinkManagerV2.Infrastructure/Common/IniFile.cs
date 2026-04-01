using System.Text;

namespace SvxlinkManagerV2.Infrastructure.Common;

/// <summary>
/// Parser et writer de fichiers INI moderne, compatible .NET 9.
/// Support natif des commentaires (# et ;) et des sections vides.
/// </summary>
public class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections;
    private readonly List<string> _comments;

    public IniFile()
    {
        _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        _comments = new List<string>();
    }

    /// <summary>
    /// Parse un fichier INI depuis un chemin.
    /// </summary>
    public static IniFile Parse(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return ParseContent(content);
    }

    /// <summary>
    /// Parse un fichier INI depuis un contenu string.
    /// </summary>
    public static IniFile ParseContent(string content)
    {
        var ini = new IniFile();
        string? currentSection = null;

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Ignorer les lignes vides
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Commentaires globaux (avant toute section)
            if ((line.StartsWith("#") || line.StartsWith(";")) && currentSection == null)
            {
                ini._comments.Add(line);
                continue;
            }

            // Ignorer les commentaires dans les sections (on ne les préserve pas pour simplifier)
            if (line.StartsWith("#") || line.StartsWith(";"))
                continue;

            // Section [NAME]
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line[1..^1].Trim();
                if (!ini._sections.ContainsKey(currentSection))
                {
                    ini._sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                continue;
            }

            // Clé=Valeur
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex > 0 && currentSection != null)
            {
                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();
                ini._sections[currentSection][key] = value;
            }
        }

        return ini;
    }

    /// <summary>
    /// Supprime une section du fichier INI.
    /// </summary>
    /// <param name="section">Nom de la section à supprimer</param>
    /// <returns>true si la section existait et a été supprimée, false sinon</returns>
    public bool RemoveSection(string section)
    {
        return _sections.Remove(section);
    }

    /// <summary>
    /// Vérifie si une section existe.
    /// </summary>
    public bool ContainsSection(string section)
    {
        return _sections.ContainsKey(section);
    }

    /// <summary>
    /// Accès aux sections via indexeur.
    /// </summary>
    public IniSection this[string section]
    {
        get
        {
            if (!_sections.ContainsKey(section))
            {
                _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            return new IniSection(_sections[section]);
        }
    }

    /// <summary>
    /// Convertit l'objet INI en string formaté.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();

        // Ajouter les commentaires globaux
        foreach (var comment in _comments)
        {
            sb.AppendLine(comment);
        }

        if (_comments.Count > 0)
            sb.AppendLine();

        // Ajouter chaque section
        foreach (var section in _sections)
        {
            sb.AppendLine($"[{section.Key}]");
            
            foreach (var kvp in section.Value)
            {
                sb.AppendLine($"{kvp.Key}={kvp.Value}");
            }
            
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Écrit le fichier INI sur disque.
    /// </summary>
    public void WriteFile(string filePath)
    {
        File.WriteAllText(filePath, ToString());
    }

    /// <summary>
    /// Écrit le fichier INI sur disque de façon asynchrone.
    /// </summary>
    public async Task WriteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await File.WriteAllTextAsync(filePath, ToString(), cancellationToken);
    }
}

/// <summary>
/// Représente une section INI avec accès par clé.
/// </summary>
public class IniSection
{
    private readonly Dictionary<string, string> _data;

    internal IniSection(Dictionary<string, string> data)
    {
        _data = data;
    }

    /// <summary>
    /// Accès aux valeurs via indexeur.
    /// </summary>
    public string this[string key]
    {
        get => _data.TryGetValue(key, out var value) ? value : string.Empty;
        set => _data[key] = value;
    }

    /// <summary>
    /// Vérifie si une clé existe.
    /// </summary>
    public bool ContainsKey(string key) => _data.ContainsKey(key);
}
