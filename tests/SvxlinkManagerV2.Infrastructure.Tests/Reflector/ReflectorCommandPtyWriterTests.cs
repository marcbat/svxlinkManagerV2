using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.Reflector;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Reflector;

/// <summary>
/// Tests de l'envoi de commandes au démon svxreflector.
/// </summary>
/// <remarks>
/// Le chemin nominal n'est pas testable hors d'un vrai pseudo-terminal : ce qui est éprouvé
/// ici, c'est ce que le service refuse d'envoyer, et ce qu'il dit quand le canal manque.
/// </remarks>
public class ReflectorCommandPtyWriterTests : IDisposable
{
    private readonly ILogger<ReflectorCommandPtyWriter> _logger =
        Substitute.For<ILogger<ReflectorCommandPtyWriter>>();

    private readonly string _configPath;

    public ReflectorCommandPtyWriterTests()
    {
        _configPath = Path.Combine(Path.GetTempPath(), $"svxreflector-{Guid.NewGuid():N}.conf");
    }

    private void WriteConfig(string content) => File.WriteAllText(_configPath, content);

    private ReflectorCommandPtyWriter CreateWriter() => new(_logger, _configPath);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendCommandAsync_WithAnEmptyCommand_ShouldFail(string command)
    {
        WriteConfig("[GLOBAL]\nCOMMAND_PTY=/tmp/reflector_ctrl\n");

        var result = await CreateWriter().SendCommandAsync(command);

        result.ShouldBeFail();
    }

    /// <summary>
    /// Le démon lit son PTY ligne par ligne : un saut de ligne dans la commande en injecterait
    /// une seconde. L'indicatif venant d'une demande déposée par un tiers, c'est une porte
    /// qu'il ne faut pas laisser ouverte.
    /// </summary>
    [Theory]
    [InlineData("CA SIGN HB9AAA\nNODE BLOCK HB9BBB 3600")]
    [InlineData("CA SIGN HB9AAA\rCFG GLOBAL ACCEPT_CALLSIGN .*")]
    public async Task SendCommandAsync_WithANewLine_ShouldRefuseToSend(string command)
    {
        WriteConfig("[GLOBAL]\nCOMMAND_PTY=/tmp/reflector_ctrl\n");

        var result = await CreateWriter().SendCommandAsync(command);

        result.ShouldBeFail(errors => errors.Head.Message.Should().Contain("saut de ligne"));
    }

    [Fact]
    public async Task SendCommandAsync_WithoutCommandPtyInTheConfiguration_ShouldSaySo()
    {
        // Réflecteur configuré avant cette fonctionnalité : la clé manque, et c'est à
        // l'utilisateur de l'ajouter.
        WriteConfig("[GLOBAL]\nLISTEN_PORT=5300\n");

        var result = await CreateWriter().SendCommandAsync("CA SIGN HB9GXP3-H");

        result.ShouldBeFail(errors => errors.Head.Message.Should().Contain("COMMAND_PTY"));
    }

    [Fact]
    public async Task SendCommandAsync_WithoutAnyConfigurationFile_ShouldSaySo()
    {
        var result = await CreateWriter().SendCommandAsync("CA SIGN HB9GXP3-H");

        result.ShouldBeFail(errors => errors.Head.Message.Should().Contain("COMMAND_PTY"));
    }

    [Fact]
    public async Task SendCommandAsync_WhenThePtyIsMissing_ShouldPointAtTheDaemon()
    {
        // Le PTY n'existe que tant que le démon tourne : c'est le diagnostic utile.
        WriteConfig($"[GLOBAL]\nCOMMAND_PTY={Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))}\n");

        var result = await CreateWriter().SendCommandAsync("CA SIGN HB9GXP3-H");

        result.ShouldBeFail(errors => errors.Head.Message.Should().Contain("introuvable"));
    }

    [Fact]
    public async Task SendCommandAsync_ShouldRereadTheConfigurationOnEveryCall()
    {
        // La configuration peut changer sans que l'application redémarre.
        var writer = CreateWriter();
        WriteConfig("[GLOBAL]\nLISTEN_PORT=5300\n");
        var before = await writer.SendCommandAsync("CA SIGN HB9GXP3-H");

        WriteConfig("[GLOBAL]\nCOMMAND_PTY=/tmp/absent-mais-declare\n");
        var after = await writer.SendCommandAsync("CA SIGN HB9GXP3-H");

        before.ShouldBeFail(errors => errors.Head.Message.Should().Contain("COMMAND_PTY"));
        after.ShouldBeFail(errors => errors.Head.Message.Should().Contain("introuvable"));
    }

    public void Dispose()
    {
        try { File.Delete(_configPath); } catch { /* rien à nettoyer */ }
    }
}
