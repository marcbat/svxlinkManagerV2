using FluentAssertions;
using LanguageExt;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Presentation.Services;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Presentation.Tests.Services;

/// <summary>
/// Tests unitaires pour ValidationHelper
/// </summary>
public class ValidationHelperTests
{
    [Fact]
    public void GetErrorMessages_WithSuccessValidation_ShouldReturnEmpty()
    {
        // Arrange
        var validation = Success<Error, string>("test");

        // Act
        var messages = ValidationHelper.GetErrorMessages(validation);

        // Assert
        messages.Should().BeEmpty();
    }

    [Fact]
    public void GetErrorMessages_WithSingleError_ShouldReturnOneMessage()
    {
        // Arrange
        var error = Error.Validation("TEST_ERROR", "Test error message");
        var validation = Fail<Error, string>(error);

        // Act
        var messages = ValidationHelper.GetErrorMessages(validation);

        // Assert
        messages.Should().ContainSingle()
            .Which.Should().Be("Test error message");
    }

    [Fact]
    public void GetErrorMessages_WithMultipleErrors_ShouldReturnAllMessages()
    {
        // Arrange
        var error1 = Error.Validation("ERROR_1", "First error");
        var error2 = Error.Validation("ERROR_2", "Second error");
        var error3 = Error.Validation("ERROR_3", "Third error");
        var validation = Fail<Error, string>(Seq(error1, error2, error3));

        // Act
        var messages = ValidationHelper.GetErrorMessages(validation);

        // Assert
        messages.Should().HaveCount(3);
        messages.Should().Contain("First error");
        messages.Should().Contain("Second error");
        messages.Should().Contain("Third error");
    }

    [Fact]
    public void GetFirstErrorMessage_WithSuccessValidation_ShouldReturnNull()
    {
        // Arrange
        var validation = Success<Error, int>(42);

        // Act
        var message = ValidationHelper.GetFirstErrorMessage(validation);

        // Assert
        message.Should().BeNull();
    }

    [Fact]
    public void GetFirstErrorMessage_WithSingleError_ShouldReturnMessage()
    {
        // Arrange
        var error = Error.Validation("TEST_ERROR", "Test error message");
        var validation = Fail<Error, string>(error);

        // Act
        var message = ValidationHelper.GetFirstErrorMessage(validation);

        // Assert
        message.Should().Be("Test error message");
    }

    [Fact]
    public void GetFirstErrorMessage_WithMultipleErrors_ShouldReturnFirstMessage()
    {
        // Arrange
        var error1 = Error.Validation("ERROR_1", "First error");
        var error2 = Error.Validation("ERROR_2", "Second error");
        var validation = Fail<Error, string>(Seq(error1, error2));

        // Act
        var message = ValidationHelper.GetFirstErrorMessage(validation);

        // Assert
        message.Should().Be("First error");
    }

    [Fact]
    public void IsValid_WithSuccessValidation_ShouldReturnTrue()
    {
        // Arrange
        var validation = Success<Error, string>("success");

        // Act
        var isValid = ValidationHelper.IsValid(validation);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithFailedValidation_ShouldReturnFalse()
    {
        // Arrange
        var error = Error.Validation("ERROR", "Error message");
        var validation = Fail<Error, string>(error);

        // Act
        var isValid = ValidationHelper.IsValid(validation);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void GetErrorCodes_WithSuccessValidation_ShouldReturnEmpty()
    {
        // Arrange
        var validation = Success<Error, string>("test");

        // Act
        var codes = ValidationHelper.GetErrorCodes(validation);

        // Assert
        codes.Should().BeEmpty();
    }

    [Fact]
    public void GetErrorCodes_WithMultipleErrors_ShouldReturnAllCodes()
    {
        // Arrange
        var error1 = Error.Validation("ERROR_1", "First error");
        var error2 = Error.Validation("ERROR_2", "Second error");
        var validation = Fail<Error, string>(Seq(error1, error2));

        // Act
        var codes = ValidationHelper.GetErrorCodes(validation);

        // Assert
        codes.Should().HaveCount(2);
        codes.Should().Contain("ERROR_1");
        codes.Should().Contain("ERROR_2");
    }

    [Fact]
    public void GetErrors_WithSuccessValidation_ShouldReturnEmpty()
    {
        // Arrange
        var validation = Success<Error, string>("test");

        // Act
        var errors = ValidationHelper.GetErrors(validation);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void GetErrors_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var error1 = Error.Validation("ERROR_1", "First error");
        var error2 = Error.Validation("ERROR_2", "Second error");
        var validation = Fail<Error, string>(Seq(error1, error2));

        // Act
        var errors = ValidationHelper.GetErrors(validation);

        // Assert
        errors.Should().HaveCount(2);
        errors.Should().Contain(e => e.Code == "ERROR_1" && e.Message == "First error");
        errors.Should().Contain(e => e.Code == "ERROR_2" && e.Message == "Second error");
    }
}
