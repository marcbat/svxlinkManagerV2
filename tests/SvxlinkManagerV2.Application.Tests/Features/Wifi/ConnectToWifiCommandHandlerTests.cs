using FluentAssertions;
using LanguageExt;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Wifi;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Tests.Features.Wifi;

/// <summary>
/// Tests unitaires pour ConnectToWifiCommandHandler.
/// </summary>
public class ConnectToWifiCommandHandlerTests
{
    private readonly IWifiService _wifiService;
    private readonly ConnectToWifiCommandHandler _handler;

    public ConnectToWifiCommandHandlerTests()
    {
        _wifiService = Substitute.For<IWifiService>();
        _handler = new ConnectToWifiCommandHandler(_wifiService);
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldCallConnectAsync()
    {
        // Arrange
        _wifiService.ConnectAsync("HomeNetwork", "password123", Arg.Any<CancellationToken>())
            .Returns(Validation<Error, Unit>.Success(Unit.Default));

        var command = new ConnectToWifiCommand("HomeNetwork", "password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _wifiService.Received(1).ConnectAsync("HomeNetwork", "password123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSsidIsEmpty_ShouldReturnFailureWithoutCallingService()
    {
        // Arrange
        var command = new ConnectToWifiCommand("", "password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFail.Should().BeTrue();
        await _wifiService.DidNotReceive().ConnectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSsidIsWhitespace_ShouldReturnFailureWithoutCallingService()
    {
        // Arrange
        var command = new ConnectToWifiCommand("   ", "password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFail.Should().BeTrue();
        await _wifiService.DidNotReceive().ConnectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenServiceFails_ShouldReturnFailure()
    {
        // Arrange
        _wifiService.ConnectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("WIFI_COMMAND_FAILED", "Connexion refusée").ToFailure<Unit>());

        var command = new ConnectToWifiCommand("HomeNetwork", "wrongpassword");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldPassPasswordToServiceUnmodified()
    {
        // Arrange - Vérifier que le handler transmet le mot de passe tel quel au service
        // (la responsabilité de ne pas logger le mot de passe est celle du service)
        var capturedPassword = string.Empty;

        _wifiService.ConnectAsync(Arg.Any<string>(), Arg.Do<string>(p => capturedPassword = p), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, Unit>.Success(Unit.Default));

        var command = new ConnectToWifiCommand("TestNetwork", "secretpassword");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - Le mot de passe est transmis tel quel au service (pas transformé)
        capturedPassword.Should().Be("secretpassword");
    }
}
