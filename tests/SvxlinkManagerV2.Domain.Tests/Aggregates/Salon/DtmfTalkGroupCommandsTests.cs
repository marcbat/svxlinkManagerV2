using FluentAssertions;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.Salon;

public class DtmfTalkGroupCommandsTests
{
    #region Construction des séquences

    [Fact]
    public void SelectTalkGroup_ShouldBuildPrefixedSelectCommand()
    {
        DtmfTalkGroupCommands.SelectTalkGroup(240).Should().Be("351240");
    }

    [Fact]
    public void RequestQsy_ShouldBuildPrefixedQsyCommand()
    {
        DtmfTalkGroupCommands.RequestQsy(2403).Should().Be("3522403");
    }

    [Fact]
    public void TemporaryMonitor_ShouldBuildPrefixedMonitorCommand()
    {
        DtmfTalkGroupCommands.TemporaryMonitor(2404).Should().Be("3542404");
    }

    [Fact]
    public void ShortCommands_ShouldOnlyCarryTheSubCommand()
    {
        DtmfTalkGroupCommands.Status.Should().Be("35*");
        DtmfTalkGroupCommands.PreviousTalkGroup.Should().Be("351");
        DtmfTalkGroupCommands.RandomQsy.Should().Be("352");
        DtmfTalkGroupCommands.FollowLastQsy.Should().Be("353");
    }

    #endregion

    #region Reconnaissance

    [Theory]
    [InlineData("35*")]
    [InlineData("351")]
    [InlineData("351240")]
    [InlineData("352")]
    [InlineData("3522403")]
    [InlineData("353")]
    [InlineData("3542404")]
    [InlineData(" 351240 ")]
    public void IsTalkGroupCommand_WithTalkGroupSequence_ShouldReturnTrue(string command)
    {
        DtmfTalkGroupCommands.IsTalkGroupCommand(command).Should().BeTrue();
    }

    [Theory]
    [InlineData("35")]      // sous-commande vide : désactiverait le lien
    [InlineData("350")]     // sous-commande 0 : inconnue de ReflectorLogic
    [InlineData("355")]     // sous-commande hors 1-4
    [InlineData("96")]      // code salon
    [InlineData("310")]     // commande système
    [InlineData("399")]     // commande interne
    [InlineData("*")]
    [InlineData("")]
    [InlineData(null)]
    public void IsTalkGroupCommand_WithOtherSequence_ShouldReturnFalse(string? command)
    {
        DtmfTalkGroupCommands.IsTalkGroupCommand(command).Should().BeFalse();
    }

    #endregion

    #region Cohérence avec les autres plages

    [Fact]
    public void Prefix_ShouldBeUsableAsASvxLinkCommandField()
    {
        // LinkManager::addLogic ne crée l'objet de commande que si atoi(cmd) > 0.
        int.TryParse(DtmfTalkGroupCommands.Prefix, out var command).Should().BeTrue();
        command.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Prefix_ShouldNotCollideWithSystemOrAnnouncementCodes()
    {
        var prefixed = Enumerable.Range(1, 9999).Where(DtmfCodeRanges.IsTalkGroupPrefixed).ToList();

        prefixed.Should().NotContain(DtmfSystemCommands.All.Select(c => c.Code));
        prefixed.Should().NotContain([301, 302, 303, 304, 305, 306, 307, 398, 399]);
    }

    [Fact]
    public void All_ShouldDocumentEverySubCommandOfReflectorLogic()
    {
        DtmfTalkGroupCommands.All.Should().HaveCount(7);
        DtmfTalkGroupCommands.All.Should().OnlyContain(c => c.Pattern.StartsWith(DtmfTalkGroupCommands.Prefix));
        DtmfTalkGroupCommands.All.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Description));
    }

    #endregion
}
