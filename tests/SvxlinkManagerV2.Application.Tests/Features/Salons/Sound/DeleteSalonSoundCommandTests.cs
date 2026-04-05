using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.Sound;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons.Sound;

public class DeleteSalonSoundCommandTests
{
    private readonly ISalonRepository _salonRepository = Substitute.For<ISalonRepository>();
    private readonly ISoundRepository _soundRepository = Substitute.For<ISoundRepository>();
    private readonly IActiveSessionTracker _tracker = Substitute.For<IActiveSessionTracker>();

    [Fact]
    public async Task Handle_WithExistingSound_ShouldDeleteAndRemove()
    {
        var salonId = Guid.NewGuid();
        var soundId = Guid.NewGuid();
        var salon = SalonSoundTestHelpers.CreateValidSalonAggregate(salonId, soundId: soundId);

        _tracker.IsSalonActive(salonId).Returns(false);
        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());
        _soundRepository.HardDeleteAsync(soundId, Arg.Any<CancellationToken>()).Returns(unit.ToSuccess());
        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>()).Returns(unit.ToSuccess());

        var result = await new DeleteSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new DeleteSalonSoundCommand(salonId), CancellationToken.None);

        result.ShouldBeSuccess();
        await _soundRepository.Received(1).HardDeleteAsync(soundId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoSound_ShouldFail()
    {
        var salonId = Guid.NewGuid();
        var salon = SalonSoundTestHelpers.CreateValidSalonAggregate(salonId);

        _tracker.IsSalonActive(salonId).Returns(false);
        _salonRepository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());

        var result = await new DeleteSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new DeleteSalonSoundCommand(salonId), CancellationToken.None);

        result.ShouldBeFail(errors => errors.Should().Contain(e => e.Code == "SALON_NO_SOUND"));
    }

    [Fact]
    public async Task Handle_WhenSalonIsActive_ShouldFail()
    {
        var salonId = Guid.NewGuid();
        _tracker.IsSalonActive(salonId).Returns(true);

        var result = await new DeleteSalonSoundCommandHandler(_salonRepository, _soundRepository, _tracker)
            .Handle(new DeleteSalonSoundCommand(salonId), CancellationToken.None);

        result.ShouldBeFail(errors => errors.Should().Contain(e => e.Code == "SALON_ACTIVE"));
    }
}
