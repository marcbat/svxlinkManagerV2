using FluentAssertions;
using LanguageExt;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.Sound;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons.Sound;

public class GetSalonSoundQueryTests
{
    private readonly ISalonRepository _salonRepository = Substitute.For<ISalonRepository>();
    private readonly ISoundRepository _soundRepository = Substitute.For<ISoundRepository>();

    [Fact]
    public async Task Handle_WhenSalonHasSound_ShouldReturnSummary()
    {
        var salonId = Guid.NewGuid();
        var soundId = Guid.NewGuid();
        var salon = SalonSoundTestHelpers.CreateValidSalonAggregate(salonId, soundId: soundId);
        var sound = SalonSoundTestHelpers.CreateValidSoundAggregate(soundId, "annonce");

        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());
        _soundRepository.GetByIdAsync(soundId, Arg.Any<CancellationToken>()).Returns(sound.ToSuccess());

        var result = await new GetSalonSoundQueryHandler(_salonRepository, _soundRepository)
            .Handle(new GetSalonSoundQuery(salonId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(soundId);
        result.Name.Should().Be("annonce");
    }

    [Fact]
    public async Task Handle_WhenNoSound_ShouldReturnNull()
    {
        var salonId = Guid.NewGuid();
        var salon = SalonSoundTestHelpers.CreateValidSalonAggregate(salonId);

        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());

        var result = await new GetSalonSoundQueryHandler(_salonRepository, _soundRepository)
            .Handle(new GetSalonSoundQuery(salonId), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenSalonNotFound_ShouldReturnNull()
    {
        var salonId = Guid.NewGuid();
        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Salon", salonId).ToFailure<SalonAggregate>());

        var result = await new GetSalonSoundQueryHandler(_salonRepository, _soundRepository)
            .Handle(new GetSalonSoundQuery(salonId), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenSoundNotFound_ShouldReturnNull()
    {
        var salonId = Guid.NewGuid();
        var soundId = Guid.NewGuid();
        var salon = SalonSoundTestHelpers.CreateValidSalonAggregate(salonId, soundId: soundId);

        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());
        _soundRepository.GetByIdAsync(soundId, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Sound", soundId).ToFailure<SoundAggregate>());

        var result = await new GetSalonSoundQueryHandler(_salonRepository, _soundRepository)
            .Handle(new GetSalonSoundQuery(salonId), CancellationToken.None);

        result.Should().BeNull();
    }
}
