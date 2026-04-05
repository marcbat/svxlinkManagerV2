using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.Sound;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons.Sound;

public class UploadSalonSoundCommandTests
{
    private readonly ISalonRepository _salonRepository = Substitute.For<ISalonRepository>();
    private readonly ISoundRepository _soundRepository = Substitute.For<ISoundRepository>();
    private readonly IActiveSessionTracker _tracker = Substitute.For<IActiveSessionTracker>();

    [Fact]
    public async Task Handle_WithValidCommand_ShouldSucceed()
    {
        var salonId = Guid.NewGuid();
        var salon = SalonSoundTestHelpers.CreateValidSalonAggregate(salonId);
        var wav = SalonSoundTestHelpers.CreateValidWavFile();

        _tracker.IsSalonActive(salonId).Returns(false);
        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());
        _soundRepository.SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>()).Returns(unit.ToSuccess());
        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>()).Returns(unit.ToSuccess());

        var result = await new UploadSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new UploadSalonSoundCommand(salonId, "test", wav), CancellationToken.None);

        result.ShouldBeSuccess(id => id.Should().NotBeEmpty());
    }

    [Fact]
    public async Task Handle_WhenSalonIsActive_ShouldFail()
    {
        var salonId = Guid.NewGuid();
        _tracker.IsSalonActive(salonId).Returns(true);

        var result = await new UploadSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new UploadSalonSoundCommand(salonId, "test", SalonSoundTestHelpers.CreateValidWavFile()), CancellationToken.None);

        result.ShouldBeFail(errors => errors.Should().Contain(e => e.Code == "SALON_ACTIVE"));
    }

    [Fact]
    public async Task Handle_WhenSalonAlreadyHasSound_ShouldFail()
    {
        var salonId = Guid.NewGuid();
        var salon = SalonSoundTestHelpers.CreateValidSalonAggregate(salonId, soundId: Guid.NewGuid());

        _tracker.IsSalonActive(salonId).Returns(false);
        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());

        var result = await new UploadSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new UploadSalonSoundCommand(salonId, "test", SalonSoundTestHelpers.CreateValidWavFile()), CancellationToken.None);

        result.ShouldBeFail(errors => errors.Should().Contain(e => e.Code == "SALON_SOUND_EXISTS"));
    }

    [Fact]
    public async Task Handle_WhenSalonNotFound_ShouldFail()
    {
        var salonId = Guid.NewGuid();
        _tracker.IsSalonActive(salonId).Returns(false);
        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Salon", salonId).ToFailure<SalonAggregate>());

        var result = await new UploadSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new UploadSalonSoundCommand(salonId, "test", SalonSoundTestHelpers.CreateValidWavFile()), CancellationToken.None);

        result.ShouldBeFail(errors => errors.Should().Contain(e => e.Code.Contains("NOT_FOUND")));
    }

    [Fact]
    public async Task Handle_WithInvalidWav_ShouldFail()
    {
        var salonId = Guid.NewGuid();
        var salon = SalonSoundTestHelpers.CreateValidSalonAggregate(salonId);

        _tracker.IsSalonActive(salonId).Returns(false);
        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());

        var result = await new UploadSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new UploadSalonSoundCommand(salonId, "test", new byte[] { 1, 2, 3 }), CancellationToken.None);

        result.ShouldBeFail();
    }
}
