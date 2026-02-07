using FluentAssertions;
using LanguageExt;
using SvxlinkManagerV2.Application.Features.Validation;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Tests.Features.Validation;

/// <summary>
/// Tests unitaires pour ValidateInputCommandHandler
/// Démontre l'utilisation du Result Pattern avec Validation
/// </summary>
public class ValidateInputCommandHandlerTests
{
    [Fact]
    public void Handle_Should_Return_Success_When_Input_IsValid()
    {
        // Arrange
        var command = new ValidateInputCommand("ValidInput");

        // Act
        var result = ValidateInputCommandHandler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: value => value.Should().Be("VALIDINPUT"),
            Fail: _ => Assert.Fail("Expected success")
        );
    }

    [Fact]
    public void Handle_Should_Return_Uppercase_When_Input_IsValid()
    {
        // Arrange
        var command = new ValidateInputCommand("test");

        // Act
        var result = ValidateInputCommandHandler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: value => value.Should().Be("TEST"),
            Fail: _ => Assert.Fail("Expected success")
        );
    }

    [Fact]
    public void Handle_Should_Return_Failure_When_Input_IsEmpty()
    {
        // Arrange
        var command = new ValidateInputCommand("");

        // Act
        var result = ValidateInputCommandHandler.Handle(command);

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void Handle_Should_Return_Failure_When_Input_IsTooShort()
    {
        // Arrange
        var command = new ValidateInputCommand("ab");

        // Act
        var result = ValidateInputCommandHandler.Handle(command);

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void Handle_Should_Return_Failure_When_Input_IsTooLong()
    {
        // Arrange
        var command = new ValidateInputCommand(new string('a', 51));

        // Act
        var result = ValidateInputCommandHandler.Handle(command);

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void Handle_Should_Accept_Input_At_Minimum_Length()
    {
        // Arrange
        var command = new ValidateInputCommand("abc");

        // Act
        var result = ValidateInputCommandHandler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: value => value.Should().Be("ABC"),
            Fail: _ => Assert.Fail("Expected success")
        );
    }

    [Fact]
    public void Handle_Should_Accept_Input_At_Maximum_Length()
    {
        // Arrange
        var command = new ValidateInputCommand(new string('a', 50));

        // Act
        var result = ValidateInputCommandHandler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateAndGetLength_Should_Return_Length_When_Input_IsValid()
    {
        // Arrange
        var input = "test";

        // Act
        var result = ValidateInputCommandHandler.ValidateAndGetLength(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: length => length.Should().Be(4),
            Fail: _ => Assert.Fail("Expected success")
        );
    }

    [Fact]
    public void ValidateAndGetLength_Should_Return_Failure_When_Input_IsEmpty()
    {
        // Arrange
        var input = "";

        // Act
        var result = ValidateInputCommandHandler.ValidateAndGetLength(input);

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void ValidateAndGetLength_Should_Return_Failure_When_Length_IsInvalid()
    {
        // Arrange
        var input = "ab"; // Too short

        // Act
        var result = ValidateInputCommandHandler.ValidateAndGetLength(input);

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void Match_Should_Execute_Success_Branch_When_Valid()
    {
        // Arrange
        var command = new ValidateInputCommand("test");
        string? result = null;

        // Act
        ValidateInputCommandHandler.Handle(command).Match(
            Succ: value => result = value,
            Fail: _ => result = "error"
        );

        // Assert
        result.Should().Be("TEST");
    }

    [Fact]
    public void Match_Should_Execute_Failure_Branch_When_Invalid()
    {
        // Arrange
        var command = new ValidateInputCommand("");
        string? result = null;

        // Act
        ValidateInputCommandHandler.Handle(command).Match(
            Succ: _ => result = "success",
            Fail: errors => result = $"Errors: {errors.Count()}"
        );

        // Assert
        result.Should().StartWith("Errors:");
    }
}
