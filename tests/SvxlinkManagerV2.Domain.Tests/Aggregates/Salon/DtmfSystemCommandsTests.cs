using FluentAssertions;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.Salon;

/// <summary>
/// Tests unitaires pour DtmfSystemCommands
/// </summary>
public class DtmfSystemCommandsTests
{
    [Fact]
    public void Codes_ShouldMatchSpecification()
    {
        DtmfSystemCommands.DefaultSalon.Should().Be(310);
        DtmfSystemCommands.Disconnect.Should().Be(311);
        DtmfSystemCommands.NextSalon.Should().Be(312);
        DtmfSystemCommands.PreviousSalon.Should().Be(313);
        DtmfSystemCommands.RestartDaemon.Should().Be(320);
    }

    [Theory]
    [InlineData(310)]
    [InlineData(311)]
    [InlineData(312)]
    [InlineData(313)]
    [InlineData(320)]
    public void IsSystemCommand_WithSystemCode_ShouldReturnTrue(int code)
    {
        DtmfSystemCommands.IsSystemCommand(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(96)]
    [InlineData(300)]
    [InlineData(301)]
    [InlineData(314)]
    [InlineData(321)]
    [InlineData(399)]
    [InlineData(9999)]
    public void IsSystemCommand_WithNonSystemCode_ShouldReturnFalse(int code)
    {
        DtmfSystemCommands.IsSystemCommand(code).Should().BeFalse();
    }

    [Fact]
    public void All_ShouldExposeEveryCommandOrderedByCode()
    {
        DtmfSystemCommands.All.Select(c => c.Code)
            .Should().Equal(310, 311, 312, 313, 320);

        DtmfSystemCommands.All.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Description));
    }

    [Fact]
    public void All_ShouldOnlyContainCodesReservedFromSalonAssignment()
    {
        // Les codes système appartiennent à la plage d'annonces 300-399, donc réservés :
        // ils ne peuvent pas être attribués comme code DTMF de salon.
        DtmfSystemCommands.All.Should().OnlyContain(c => DtmfCodeRanges.IsReserved(c.Code));
        DtmfSystemCommands.All.Should().OnlyContain(c => !DtmfCodeRanges.IsValidForSalon(c.Code));
    }
}
