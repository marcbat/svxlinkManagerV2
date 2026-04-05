using FluentAssertions;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.SvxLink;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests unitaires pour DtmfPtyWriter.
/// </summary>
public class DtmfPtyWriterTests
{
    private readonly ILogger<DtmfPtyWriter> _logger;

    public DtmfPtyWriterTests()
    {
        _logger = Substitute.For<ILogger<DtmfPtyWriter>>();
    }

    // -------------------------------------------------------------------------
    // Validation des paramètres
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendCommandAsync_WithEmptyCommand_ShouldReturnFailure()
    {
        var writer = new DtmfPtyWriter(_logger);

        var result = await writer.SendCommandAsync(string.Empty);

        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task SendCommandAsync_WithWhitespaceCommand_ShouldReturnFailure()
    {
        var writer = new DtmfPtyWriter(_logger);

        var result = await writer.SendCommandAsync("   ");

        result.IsFail.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // PTY manquant
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendCommandAsync_WhenPtyDoesNotExist_ShouldReturnFailure()
    {
        var nonExistentPty = $"/tmp/non_existent_pty_{Guid.NewGuid()}";
        var writer = new DtmfPtyWriter(_logger, nonExistentPty);

        var result = await writer.SendCommandAsync("399");

        result.IsFail.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Écriture correcte dans le PTY
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("399", "399#")]
    [InlineData("300", "300#")]
    [InlineData("301", "301#")]
    public async Task SendCommandAsync_WhenPtyExists_ShouldWriteCommandWithHash(string command, string expectedPayload)
    {
        // Arrange : créer un fichier temporaire pour simuler le PTY
        var tmpPty = Path.Combine(Path.GetTempPath(), $"dtmf_test_{Guid.NewGuid()}");
        await File.WriteAllTextAsync(tmpPty, string.Empty);

        try
        {
            var writer = new DtmfPtyWriter(_logger, tmpPty);

            // Act
            var result = await writer.SendCommandAsync(command);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var content = await File.ReadAllTextAsync(tmpPty);
            content.Should().Contain(expectedPayload);
        }
        finally
        {
            if (File.Exists(tmpPty))
                File.Delete(tmpPty);
        }
    }

    // -------------------------------------------------------------------------
    // Constante du chemin PTY par défaut
    // -------------------------------------------------------------------------

    [Fact]
    public void DefaultPtyPath_ShouldBeCorrect()
    {
        DtmfPtyWriter.DefaultPtyPath.Should().Be("/tmp/dtmf_uhf");
    }
}
