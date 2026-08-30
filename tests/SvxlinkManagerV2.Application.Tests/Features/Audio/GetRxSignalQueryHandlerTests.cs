using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Audio;
using SvxlinkManagerV2.Application.Features.Audio.GetRxSignal;
using SvxlinkManagerV2.Application.Interfaces;
using LanguageExtError = LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Application.Tests.Features.Audio;

/// <summary>
/// Tests unitaires de GetRxSignalQueryHandler.
/// </summary>
public class GetRxSignalQueryHandlerTests
{
    private readonly ISA818Service _sa818Service = Substitute.For<ISA818Service>();
    private readonly IRxDistortionService _distortionService = Substitute.For<IRxDistortionService>();
    private readonly GetRxSignalQueryHandler _handler;

    public GetRxSignalQueryHandlerTests()
    {
        _sa818Service.ReadRssiAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<LanguageExtError, int>.Success(20));

        _handler = new GetRxSignalQueryHandler(_sa818Service, _distortionService);
    }

    [Fact]
    public async Task Handle_ShouldReturnTheRawRssi()
    {
        var result = await _handler.Handle(new GetRxSignalQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto =>
        {
            dto.Rssi.Should().Be(20);
            dto.RssiError.Should().BeNull();
        });
    }

    [Fact]
    public async Task Handle_ShouldSucceedWithAnExplanation_WhenTheModuleIsSilent()
    {
        // Un module muet ne doit pas masquer le compteur d'écrêtages, qui reste exploitable.
        _sa818Service.ReadRssiAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<LanguageExtError, int>.Fail(
                Prelude.Seq1(LanguageExtError.New(500, "pas de réponse"))));
        _distortionService.DetectionCount.Returns(2);

        var result = await _handler.Handle(new GetRxSignalQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto =>
        {
            dto.Rssi.Should().BeNull();
            dto.RssiError.Should().NotBeNull();
            dto.DistortionCount.Should().Be(2);
        });
    }

    [Fact]
    public async Task Handle_ShouldCarryTheDistortionCounters()
    {
        var detectedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        _distortionService.LastDetectedAt.Returns(detectedAt);
        _distortionService.DetectionCount.Returns(4);

        var result = await _handler.Handle(new GetRxSignalQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto =>
        {
            dto.LastDistortionAt.Should().Be(detectedAt);
            dto.DistortionCount.Should().Be(4);
        });
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(128, 50)]
    [InlineData(255, 100)]
    public async Task RssiPercent_ShouldPositionTheValueOnTheModuleScale(int rssi, int expectedPercent)
    {
        _sa818Service.ReadRssiAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<LanguageExtError, int>.Success(rssi));

        var result = await _handler.Handle(new GetRxSignalQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto => dto.RssiPercent.Should().Be(expectedPercent));
    }

    [Fact]
    public void RssiPercent_ShouldBeNull_WhenTheRssiIsUnavailable()
    {
        var dto = new RxSignalDto(null, "indisponible", null, 0);

        dto.RssiPercent.Should().BeNull();
    }
}
