using FluentAssertions;
using LanguageExt;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Domain.Tests.Common;

/// <summary>
/// Tests unitaires pour ValidationExtensions
/// </summary>
public class ValidationExtensionsTests
{
    [Fact]
    public void ToSuccess_Should_Create_Successful_Validation()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.ToSuccess();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: v => v.Should().Be("test"),
            Fail: _ => Assert.Fail("Expected success")
        );
    }

    [Fact]
    public void ToFailure_Should_Create_Failed_Validation_With_Single_Error()
    {
        // Arrange
        var error = new Error("CODE", "Message");

        // Act
        var result = error.ToFailure<string>();

        // Assert
        result.IsFail.Should().BeTrue();
        result.Match(
            Succ: _ => Assert.Fail("Expected failure"),
            Fail: errors => errors.Should().Contain(error)
        );
    }

    [Fact]
    public void ToFailure_Should_Create_Failed_Validation_With_Multiple_Errors()
    {
        // Arrange
        var errors = new[]
        {
            new Error("CODE1", "Message1"),
            new Error("CODE2", "Message2")
        };

        // Act
        var result = errors.ToFailure<string>();

        // Assert
        result.IsFail.Should().BeTrue();
        result.Match(
            Succ: _ => Assert.Fail("Expected failure"),
            Fail: errs => errs.Count().Should().Be(2)
        );
    }

    [Fact]
    public void ValidateNotEmpty_Should_Return_Success_When_String_IsValid()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.ValidateNotEmpty("CODE", "Message");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateNotEmpty_Should_Return_Failure_When_String_IsEmpty()
    {
        // Arrange
        var value = "";

        // Act
        var result = value.ValidateNotEmpty("EMPTY", "Value is empty");

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void ValidateNotEmpty_Should_Return_Failure_When_String_IsNull()
    {
        // Arrange
        string? value = null;

        // Act
        var result = value.ValidateNotEmpty("NULL", "Value is null");

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void ValidateThat_Should_Return_Success_When_Predicate_IsTrue()
    {
        // Arrange
        var value = 10;

        // Act
        var result = value.ValidateThat(
            v => v > 5,
            "TOO_SMALL",
            "Value must be greater than 5");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateThat_Should_Return_Failure_When_Predicate_IsFalse()
    {
        // Arrange
        var value = 3;

        // Act
        var result = value.ValidateThat(
            v => v > 5,
            "TOO_SMALL",
            "Value must be greater than 5");

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void ValidateNotEmpty_Guid_Should_Return_Success_When_Guid_IsNotEmpty()
    {
        // Arrange
        var value = Guid.NewGuid();

        // Act
        var result = value.ValidateNotEmpty("id");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateNotEmpty_Guid_Should_Return_Failure_When_Guid_IsEmpty()
    {
        // Arrange
        var value = Guid.Empty;

        // Act
        var result = value.ValidateNotEmpty("id");

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void Chaining_Validations_With_Bind_Should_Work()
    {
        // Arrange
        var input = "test";

        // Act
        var result = input
            .ValidateNotEmpty("EMPTY", "Cannot be empty")
            .Bind(value => value.ValidateThat(
                v => v.Length >= 3,
                "TOO_SHORT",
                "Must be at least 3 characters"))
            .Map(value => value.ToUpper());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: v => v.Should().Be("TEST"),
            Fail: _ => Assert.Fail("Expected success")
        );
    }

    [Fact]
    public void Chaining_Validations_Should_Stop_At_First_Failure()
    {
        // Arrange
        var input = "";

        // Act
        var result = input
            .ValidateNotEmpty("EMPTY", "Cannot be empty")
            .Bind(value => value.ValidateThat(
                v => v.Length >= 3,
                "TOO_SHORT",
                "Must be at least 3 characters"));

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    public void ValidateThat_Should_Fail_For_Short_Strings(string input)
    {
        // Act
        var result = input.ValidateThat(
            v => v.Length >= 3,
            "TOO_SHORT",
            "Must be at least 3 characters");

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcd")]
    [InlineData("abcdefg")]
    public void ValidateThat_Should_Succeed_For_Valid_Strings(string input)
    {
        // Act
        var result = input.ValidateThat(
            v => v.Length >= 3,
            "TOO_SHORT",
            "Must be at least 3 characters");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
