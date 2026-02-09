namespace SvxlinkManagerV2.Presentation.Services;

/// <summary>
/// Service pour la gestion et le formatage des données audio
/// </summary>
public class AudioService
{
    /// <summary>
    /// Convertit un tableau de bytes audio en Data URI pour utilisation dans un élément HTML audio
    /// </summary>
    /// <param name="audioData">Données audio en bytes (format WAV)</param>
    /// <returns>Data URI au format data:audio/wav;base64,...</returns>
    public string ConvertToDataUri(byte[] audioData)
    {
        if (audioData == null || audioData.Length == 0)
            return string.Empty;

        var base64 = Convert.ToBase64String(audioData);
        return $"data:audio/wav;base64,{base64}";
    }

    /// <summary>
    /// Formate une durée au format mm:ss
    /// </summary>
    /// <param name="duration">Durée à formater</param>
    /// <returns>Chaîne formatée (ex: "02:35")</returns>
    public string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
    }

    /// <summary>
    /// Formate un sample rate avec l'unité Hz
    /// </summary>
    /// <param name="sampleRate">Sample rate en Hz</param>
    /// <returns>Chaîne formatée (ex: "16000 Hz")</returns>
    public string FormatSampleRate(int sampleRate)
    {
        return $"{sampleRate} Hz";
    }

    /// <summary>
    /// Formate le nombre de canaux audio avec indication Mono/Stereo
    /// </summary>
    /// <param name="channels">Nombre de canaux (1 ou 2)</param>
    /// <returns>Chaîne formatée (ex: "1 (Mono)" ou "2 (Stereo)")</returns>
    public string FormatChannels(int channels)
    {
        return channels switch
        {
            1 => "1 (Mono)",
            2 => "2 (Stereo)",
            _ => $"{channels} canaux"
        };
    }

    /// <summary>
    /// Retourne une représentation temporelle relative d'une date
    /// </summary>
    /// <param name="createdAt">Date à comparer avec maintenant</param>
    /// <returns>Chaîne formatée (ex: "Il y a 2 jours")</returns>
    public string GetRelativeTime(DateTime createdAt)
    {
        var timeSpan = DateTime.UtcNow - createdAt;

        if (timeSpan.TotalMinutes < 1)
            return "À l'instant";
        if (timeSpan.TotalMinutes < 60)
            return $"Il y a {(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes > 1 ? "s" : "")}";
        if (timeSpan.TotalHours < 24)
            return $"Il y a {(int)timeSpan.TotalHours} heure{((int)timeSpan.TotalHours > 1 ? "s" : "")}";
        if (timeSpan.TotalDays < 30)
            return $"Il y a {(int)timeSpan.TotalDays} jour{((int)timeSpan.TotalDays > 1 ? "s" : "")}";
        if (timeSpan.TotalDays < 365)
            return $"Il y a {(int)(timeSpan.TotalDays / 30)} mois";
        
        return $"Il y a {(int)(timeSpan.TotalDays / 365)} an{((int)(timeSpan.TotalDays / 365) > 1 ? "s" : "")}";
    }

    /// <summary>
    /// Formate une taille de fichier en bytes vers une chaîne lisible
    /// </summary>
    /// <param name="bytes">Taille en bytes</param>
    /// <returns>Chaîne formatée (ex: "2.5 MB")</returns>
    public string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
