using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Reflectors.BlockNode;
using SvxlinkManagerV2.Application.Features.Reflectors.SignCertificateRequest;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using LangExtError = LanguageExt.Common.Error;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Tests.Features.Reflectors;

/// <summary>
/// Tests des commandes envoyées au démon svxreflector par son PTY de contrôle.
/// </summary>
/// <remarks>
/// Les libellés exacts (<c>CA SIGN &lt;indicatif&gt;</c>, <c>NODE BLOCK &lt;indicatif&gt;
/// &lt;secondes&gt;</c>) viennent du message d'usage du binaire 25.05, relevé sur la stack le
/// 07/09/2026 — la manpage, elle, documente des commandes qui n'existent pas dans ce build.
/// </remarks>
public class CertificateAuthorityCommandTests
{
    private readonly IReflectorCommandWriter _commandWriter = Substitute.For<IReflectorCommandWriter>();

    private void GivenTheCommandIsAccepted() =>
        _commandWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));

    private void GivenTheCommandFails() =>
        _commandWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<LangExtError, Unit>.Fail(Seq1(LangExtError.New("PTY absent")))));

    private Task<Validation<Error, Unit>> Sign(string callsign) =>
        new SignCertificateRequestCommandHandler(
                _commandWriter, Substitute.For<ILogger<SignCertificateRequestCommandHandler>>())
            .Handle(new SignCertificateRequestCommand(callsign), CancellationToken.None);

    private Task<Validation<Error, Unit>> Block(string callsign, int seconds) =>
        new BlockNodeCommandHandler(
                _commandWriter, Substitute.For<ILogger<BlockNodeCommandHandler>>())
            .Handle(new BlockNodeCommand(callsign, seconds), CancellationToken.None);

    #region Signature

    [Fact]
    public async Task Sign_ShouldSendTheCaSignCommand()
    {
        GivenTheCommandIsAccepted();

        var result = await Sign("HB9GXP3-H");

        result.ShouldBeSuccess();
        await _commandWriter.Received(1).SendCommandAsync("CA SIGN HB9GXP3-H", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sign_ShouldTrimTheCallsign()
    {
        GivenTheCommandIsAccepted();

        await Sign("  HB9GXP3-H  ");

        await _commandWriter.Received(1).SendCommandAsync("CA SIGN HB9GXP3-H", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// L'indicatif vient d'une demande déposée par un tiers : il ne doit pas pouvoir
    /// transformer la commande en une autre.
    /// </summary>
    [Theory]
    [InlineData("HB9AAA HB9BBB")]
    [InlineData("HB9AAA\nNODE BLOCK HB9BBB 3600")]
    [InlineData("HB9AAA;rm -rf /")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Sign_WithAnUnacceptableCallsign_ShouldRefuseWithoutSending(string? callsign)
    {
        var result = await Sign(callsign!);

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("CERTIFICATE_CALLSIGN_INVALID"));
        await _commandWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sign_WhenTheCommandCannotBeSent_ShouldFail()
    {
        GivenTheCommandFails();

        var result = await Sign("HB9GXP3-H");

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("CERTIFICATE_SIGN_FAILED"));
    }

    #endregion

    #region Blocage

    [Fact]
    public async Task Block_ShouldSendTheNodeBlockCommand()
    {
        GivenTheCommandIsAccepted();

        var result = await Block("HB9GXP2-H", 900);

        result.ShouldBeSuccess();
        await _commandWriter.Received(1).SendCommandAsync("NODE BLOCK HB9GXP2-H 900", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(BlockNodeCommandHandler.MaximumSeconds + 1)]
    public async Task Block_WithAnUnusableDuration_ShouldRefuseWithoutSending(int seconds)
    {
        var result = await Block("HB9GXP2-H", seconds);

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("NODE_BLOCK_DURATION_INVALID"));
        await _commandWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Block_WithAnUnacceptableCallsign_ShouldRefuseWithoutSending()
    {
        var result = await Block("HB9AAA 3600\nCA SIGN HB9INTRUS", 900);

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("NODE_CALLSIGN_INVALID"));
        await _commandWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Block_WhenTheCommandCannotBeSent_ShouldFail()
    {
        GivenTheCommandFails();

        var result = await Block("HB9GXP2-H", 900);

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("NODE_BLOCK_FAILED"));
    }

    #endregion
}
