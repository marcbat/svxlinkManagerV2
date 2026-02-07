using FluentAssertions;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Tests.Common;

/// <summary>
/// Tests unitaires pour le record Error
/// </summary>
public class ErrorTests
{
    [Fact]
    public void Error_Should_Have_Code_And_Message()
    {
        // Arrange
        var code = "TEST_ERROR";
        var message = "This is a test error";

        // Act
        var error = new Error(code, message);

        // Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
    }

    [Fact]
    public void Validation_Should_Create_Error_With_Code_And_Message()
    {
        // Arrange
        var code = "INVALID_INPUT";
        var message = "Input is invalid";

        // Act
        var error = Error.Validation(code, message);

        // Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
    }

    [Fact]
    public void NotFound_Should_Create_Error_With_Standard_Format()
    {
        // Arrange
        var entityName = "User";
        var id = Guid.NewGuid();

        // Act
        var error = Error.NotFound(entityName, id);

        // Assert
        error.Code.Should().Be("USER_NOT_FOUND");
        error.Message.Should().Contain(entityName);
        error.Message.Should().Contain(id.ToString());
    }

    [Fact]
    public void Conflict_Should_Create_Error_With_Conflict_Code()
    {
        // Arrange
        var message = "Resource already exists";

        // Act
        var error = Error.Conflict(message);

        // Assert
        error.Code.Should().Be("CONFLICT");
        error.Message.Should().Be(message);
    }

    [Fact]
    public void ToString_Should_Return_Formatted_String()
    {
        // Arrange
        var error = new Error("TEST_CODE", "Test message");

        // Act
        var result = error.ToString();

        // Assert
        result.Should().Be("[TEST_CODE] Test message");
    }

    [Fact]
    public void Errors_With_Same_Values_Should_Be_Equal()
    {
        // Arrange
        var error1 = new Error("CODE", "Message");
        var error2 = new Error("CODE", "Message");

        // Act & Assert
        error1.Should().Be(error2);
        (error1 == error2).Should().BeTrue();
    }

    [Fact]
    public void Errors_With_Different_Values_Should_Not_Be_Equal()
    {
        // Arrange
        var error1 = new Error("CODE1", "Message1");
        var error2 = new Error("CODE2", "Message2");

        // Act & Assert
        error1.Should().NotBe(error2);
        (error1 == error2).Should().BeFalse();
    }

    [Theory]
    [InlineData("Salon", 123)]
    [InlineData("RadioProfil", "abc-def")]
    [InlineData("Sound", 999)]
    public void NotFound_Should_Handle_Different_Id_Types(string entityName, object id)
    {
        // Act
        var error = Error.NotFound(entityName, id);

        // Assert
        error.Code.Should().Be($"{entityName.ToUpper()}_NOT_FOUND");
        error.Message.Should().Contain(id.ToString()!);
    }
}
