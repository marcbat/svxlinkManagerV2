using FluentAssertions;
using LanguageExt;
using SvxlinkManagerV2.Application.Features.Validation;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Tests.Features.Validation;

/// <summary>
/// Tests unitaires pour ValidateInputCommandHandler
/// </summary>
public class ValidateInputCommandHandlerTests
{
    private readonly ValidateInputCommandHandler _handler = new();

    [Fact]
    public async Task Handle_Should_Return_Success_When_Input_IsValid()
    {
        var command = new ValidateInputCommand("ValidInput");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: value => value.Should().Be("VALIDINPUT"),
            Fail: _ => Assert.Fail("Expected success"));
    }

    [Fact]
    public async Task Handle_Should_Return_Uppercase_When_Input_IsValid()
    {
        var command = new ValidateInputCommand("test");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: value => value.Should().Be("TEST"),
            Fail: _ => Assert.Fail("Expected success"));
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Input_IsEmpty()
    {
        var command = new ValidateInputCommand("");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Input_IsTooShort()
    {
        var command = new ValidateInputCommand("ab");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Input_IsTooLong()
    {
        var command = new ValidateInputCommand(new string('a', 51));
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Accept_Input_At_Minimum_Length()
    {
        var command = new ValidateInputCommand("abc");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: value => value.Should().Be("ABC"),
            Fail: _ => Assert.Fail("Expected success"));
    }

    [Fact]
    public async Task Handle_Should_Accept_Input_At_Maximum_Length()
    {
        var command = new ValidateInputCommand(new string('a', 50));
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateAndGetLength_Should_Return_Length_When_Input_IsValid()
    {
        var result = ValidateInputCommandHandler.ValidateAndGetLength("test");

        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: length => length.Should().Be(4),
            Fail: _ => Assert.Fail("Expected success"));
    }

    [Fact]
    public void ValidateAndGetLength_Should_Return_Failure_When_Input_IsEmpty()
    {
        var result = ValidateInputCommandHandler.ValidateAndGetLength("");
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void ValidateAndGetLength_Should_Return_Failure_When_Length_IsInvalid()
    {
        var result = ValidateInputCommandHandler.ValidateAndGetLength("ab");
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task Match_Should_Execute_Success_Branch_When_Valid()
    {
        var command = new ValidateInputCommand("test");
        string? result = null;

        var validation = await _handler.Handle(command, CancellationToken.None);
        validation.Match(
            Succ: value => result = value,
            Fail: _ => result = "error");

        result.Should().Be("TEST");
    }

    [Fact]
    public async Task Match_Should_Execute_Failure_Branch_When_Invalid()
    {
        var command = new ValidateInputCommand("");
        string? result = null;

        var validation = await _handler.Handle(command, CancellationToken.None);
        validation.Match(
            Succ: _ => result = "success",
            Fail: errors => result = $"Errors: {errors.Count()}");

        result.Should().StartWith("Errors:");
    }
}
