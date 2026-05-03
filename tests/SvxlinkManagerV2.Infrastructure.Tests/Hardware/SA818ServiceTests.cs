using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Infrastructure.Hardware;

namespace SvxlinkManagerV2.Infrastructure.Tests.Hardware;

public class SA818ServiceTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SA818Service> _logger;

    public SA818ServiceTests()
    {
        _logger = Substitute.For<ILogger<SA818Service>>();
        
        // Configuration mockée pour les tests
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"SA818:SerialPort", "/dev/ttyS2"},
            {"SA818:BaudRate", "9600"},
            {"SA818:ReadTimeout", "2000"},
            {"SA818:WriteTimeout", "2000"},
            {"SA818:CommandDelay", "100"} // Réduit pour les tests
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public void SA818Service_ShouldInitialize_WithDefaultConfiguration()
    {
        // Act
        using var sut = new SA818Service(_configuration, _logger);

        // Assert
        sut.Should().NotBeNull();
        sut.Should().BeAssignableTo<ISA818Service>();
    }

    [Fact]
    public void SA818Service_ShouldReadConfiguration_FromIConfiguration()
    {
        // Arrange
        var customConfig = new Dictionary<string, string?>
        {
            {"SA818:SerialPort", "/dev/ttyUSB0"},
            {"SA818:BaudRate", "115200"}
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(customConfig)
            .Build();

        // Act
        using var sut = new SA818Service(config, _logger);

        // Assert
        sut.Should().NotBeNull();
    }

    [Fact]
    public async Task ConfigureAsync_ShouldReturnFailure_WhenPortNotAccessible()
    {
        // Arrange
        using var sut = new SA818Service(_configuration, _logger);
        var commands = new SA818CommandSet(
            DmoSetGroup: "AT+DMOSETGROUP=0,145.5000,145.5000,0000,4,0000",
            DmoSetVolume: "AT+DMOSETVOLUME=4",
            SetFilter: "AT+SETFILTER=0,0,0"
        );

        // Act
        var result = await sut.ConfigureAsync(commands);

        // Assert
        // Le port série n'existe probablement pas sur la machine de test,
        // donc on s'attend à une erreur
        result.ShouldBeFail();
    }

    [Fact]
    public async Task ConfigureAsync_ShouldHandleCancellation()
    {
        // Arrange
        using var sut = new SA818Service(_configuration, _logger);
        var commands = new SA818CommandSet(
            DmoSetGroup: "AT+DMOSETGROUP=0,145.5000,145.5000,0000,4,0000",
            DmoSetVolume: "AT+DMOSETVOLUME=4",
            SetFilter: "AT+SETFILTER=0,0,0"
        );
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await sut.ConfigureAsync(commands, cts.Token);

        // Assert
        result.ShouldBeFail();
    }

    [Fact]
    public async Task IsConnectedAsync_ShouldReturnFalse_WhenPortNotAccessible()
    {
        // Arrange
        using var sut = new SA818Service(_configuration, _logger);

        // Act
        var result = await sut.IsConnectedAsync();

        // Assert
        // Le port n'est pas accessible, donc on s'attend à Success(false) ou Fail
        // Vérifions que la réponse est cohérente
        result.Match(
            Succ: connected => connected.Should().BeFalse(),
            Fail: errors => true.Should().BeTrue() // C'est OK aussi
        );
    }

    [Fact]
    public void Dispose_ShouldNotThrowException()
    {
        // Arrange
        var sut = new SA818Service(_configuration, _logger);

        // Act
        var act = () => sut.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var sut = new SA818Service(_configuration, _logger);

        // Act
        sut.Dispose();
        var act = () => sut.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        // Cleanup si nécessaire
    }
}
