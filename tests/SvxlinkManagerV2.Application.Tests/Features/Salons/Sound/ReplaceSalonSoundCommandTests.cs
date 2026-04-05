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

public class ReplaceSalonSoundCommandTests
{
    private readonly ISalonRepository _salonRepository = Substitute.For<ISalonRepository>();
    private readonly ISoundRepository _soundRepository = Substitute.For<ISoundRepository>();
    private readonly IActiveSessionTracker _tracker = Substitute.For<IActiveSessionTracker>();

    [Fact]
    public async Task Handle_WithExistingSound_ShouldDeleteOldAndCreateNew()
    {
        var salonId = Guid.NewGuid();
        var oldSoundId = Guid.NewGuid();
        var salon = SalonSoundTestHelpers.CreateValidSalonAggregate(salonId, soundId: oldSoundId);

        _tracker.IsSalonActive(salonId).Returns(false);
        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());
        _soundRepository.HardDeleteAsync(oldSoundId, Arg.Any<CancellationToken>()).Returns(unit.ToSuccess());
        _soundRepository.SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>()).Returns(unit.ToSuccess());
        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>()).Returns(unit.ToSuccess());

        var result = await new ReplaceSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new ReplaceSalonSoundCommand(salonId, "new-sound", SalonSoundTestHelpers.CreateValidWavFile()), CancellationToken.None);

        result.ShouldBeSuccess(id => id.Should().NotBe(oldSoundId));
        await _soundRepository.Received(1).HardDeleteAsync(oldSoundId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutExistingSound_ShouldCreateNew()
    {
        var salonId = Guid.NewGuid();
        var salon = SalonSoundTestHelpers.CreateValidSalonAggregate(salonId);

        _tracker.IsSalonActive(salonId).Returns(false);
        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());
        _soundRepository.SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>()).Returns(unit.ToSuccess());
        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>()).Returns(unit.ToSuccess());

        var result = await new ReplaceSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new ReplaceSalonSoundCommand(salonId, "sound", SalonSoundTestHelpers.CreateValidWavFile()), CancellationToken.None);

        result.ShouldBeSuccess();
        await _soundRepository.DidNotReceive().HardDeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonIsActive_ShouldFail()
    {
        var salonId = Guid.NewGuid();
        _tracker.IsSalonActive(salonId).Returns(true);

        var result = await new ReplaceSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new ReplaceSalonSoundCommand(salonId, "test", SalonSoundTestHelpers.CreateValidWavFile()), CancellationToken.None);

        result.ShouldBeFail(errors => errors.Should().Contain(e => e.Code == "SALON_ACTIVE"));
    }
}
