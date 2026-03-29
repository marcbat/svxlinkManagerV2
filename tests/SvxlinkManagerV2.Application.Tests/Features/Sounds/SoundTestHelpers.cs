using SvxlinkManagerV2.Domain.Aggregates.Sound;

namespace SvxlinkManagerV2.Application.Tests.Features.Sounds;

/// <summary>
/// Classe d'aide partagée pour les tests Sound : création de fichiers WAV valides et d'agrégats Sound.
/// </summary>
internal static class SoundTestHelpers
{
    /// <summary>
    /// Crée un contenu de fichier WAV valide pour les tests.
    /// Format : PCM 16 bits, mono ou stéréo, sample rate configurable.
    /// </summary>
    public static byte[] CreateValidWavFile(int sampleRate = 16000, int channels = 1, int durationMs = 100)
    {
        var numSamples = sampleRate * durationMs / 1000;
        var dataSize = numSamples * channels * 2; // 16 bits = 2 bytes per sample
        var fileSize = 36 + dataSize;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(fileSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // chunk size
        writer.Write((short)1); // PCM format
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2); // byte rate
        writer.Write((short)(channels * 2)); // block align
        writer.Write((short)16); // bits per sample

        // data chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        // Silence
        for (int i = 0; i < numSamples * channels; i++)
            writer.Write((short)0);

        return ms.ToArray();
    }

    /// <summary>
    /// Crée un SoundAggregate valide avec domainEvents vidés pour les tests.
    /// </summary>
    public static SoundAggregate CreateValidAggregate(Guid id, string name = "test-sound", int sampleRate = 16000)
    {
        var result = SoundAggregate.Create(id, name, CreateValidWavFile(sampleRate));
        return result.Match(
            Succ: a => { a.ClearDomainEvents(); return a; },
            Fail: _ => throw new InvalidOperationException("Impossible de créer l'agrégat Sound de test"));
    }
}
