using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Sound;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons.Sound;

internal static class SalonSoundTestHelpers
{
    public static byte[] CreateValidWavFile(int sampleRate = 16000, int channels = 1, int durationMs = 100)
    {
        var numSamples = sampleRate * durationMs / 1000;
        var dataSize = numSamples * channels * 2;
        var fileSize = 36 + dataSize;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(fileSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);

        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        for (int i = 0; i < numSamples * channels; i++)
            writer.Write((short)0);

        return ms.ToArray();
    }

    public static SoundAggregate CreateValidSoundAggregate(Guid id, string name = "test-sound")
    {
        var result = SoundAggregate.Create(id, name, CreateValidWavFile());
        return result.Match(
            Succ: a => { a.ClearDomainEvents(); return a; },
            Fail: _ => throw new InvalidOperationException());
    }

    public static SalonAggregate CreateValidSalonAggregate(Guid id, Guid? soundId = null)
    {
        var config = new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d",
            16000, 1,
            "ref.f5kri.fr", 5300,
            "F5ABC-L", "test-auth-key",
            0,
            "F5ABC", "ModuleHelp",
            60, 60,
            null,
            "fr_FR", 0,
            145.550m, 145.550m, 136.5m, 136.5m);

        var result = SalonAggregate.Create(id, "Salon Test", false, false, config);
        var aggregate = result.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        if (soundId.HasValue)
            aggregate.AssignSound(soundId.Value);

        aggregate.ClearDomainEvents();
        return aggregate;
    }
}
