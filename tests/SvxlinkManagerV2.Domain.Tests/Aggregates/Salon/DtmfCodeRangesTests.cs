using FluentAssertions;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.Salon;

/// <summary>
/// Tests unitaires pour DtmfCodeRanges
/// </summary>
public class DtmfCodeRangesTests
{
    #region IsInModuleRange

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(19)]
    public void IsInModuleRange_WithModuleCode_ShouldReturnTrue(int code)
    {
        DtmfCodeRanges.IsInModuleRange(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(100)]
    [InlineData(-1)]
    public void IsInModuleRange_WithNonModuleCode_ShouldReturnFalse(int code)
    {
        DtmfCodeRanges.IsInModuleRange(code).Should().BeFalse();
    }

    #endregion

    #region IsInAnnounceRange

    [Theory]
    [InlineData(300)]
    [InlineData(350)]
    [InlineData(398)]
    [InlineData(399)]
    public void IsInAnnounceRange_WithAnnounceCode_ShouldReturnTrue(int code)
    {
        DtmfCodeRanges.IsInAnnounceRange(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(299)]
    [InlineData(400)]
    [InlineData(1)]
    [InlineData(9999)]
    public void IsInAnnounceRange_WithNonAnnounceCode_ShouldReturnFalse(int code)
    {
        DtmfCodeRanges.IsInAnnounceRange(code).Should().BeFalse();
    }

    #endregion

    #region IsReserved

    [Theory]
    [InlineData(1)]
    [InlineData(19)]
    [InlineData(300)]
    [InlineData(399)]
    public void IsReserved_WithReservedCode_ShouldReturnTrue(int code)
    {
        DtmfCodeRanges.IsReserved(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(299)]
    [InlineData(400)]
    [InlineData(9999)]
    public void IsReserved_WithNonReservedCode_ShouldReturnFalse(int code)
    {
        DtmfCodeRanges.IsReserved(code).Should().BeFalse();
    }

    #endregion

    #region IsValidForSalon

    [Theory]
    [InlineData(20)]
    [InlineData(96)]
    [InlineData(100)]
    [InlineData(299)]
    [InlineData(400)]
    [InlineData(1000)]
    [InlineData(9999)]
    public void IsValidForSalon_WithValidSalonCode_ShouldReturnTrue(int code)
    {
        DtmfCodeRanges.IsValidForSalon(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(19)]
    [InlineData(300)]
    [InlineData(399)]
    [InlineData(10000)]
    [InlineData(-1)]
    [InlineData(35)]
    [InlineData(3500)]
    [InlineData(3599)]
    public void IsValidForSalon_WithInvalidSalonCode_ShouldReturnFalse(int code)
    {
        DtmfCodeRanges.IsValidForSalon(code).Should().BeFalse();
    }

    #endregion

    #region IsTalkGroupPrefixed

    [Theory]
    [InlineData(35)]
    [InlineData(350)]
    [InlineData(359)]
    [InlineData(3500)]
    [InlineData(3599)]
    public void IsTalkGroupPrefixed_WithPrefixedCode_ShouldReturnTrue(int code)
    {
        DtmfCodeRanges.IsTalkGroupPrefixed(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(34)]
    [InlineData(36)]
    [InlineData(305)]
    [InlineData(3499)]
    [InlineData(3600)]
    [InlineData(0)]
    [InlineData(-35)]
    public void IsTalkGroupPrefixed_WithOtherCode_ShouldReturnFalse(int code)
    {
        DtmfCodeRanges.IsTalkGroupPrefixed(code).Should().BeFalse();
    }

    [Theory]
    [InlineData(96)]
    [InlineData(97)]
    [InlineData(98)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(210)]
    [InlineData(1000)]
    public void SeededSalonCodes_ShouldRemainValid_AfterReservingTheTalkGroupPrefix(int code)
    {
        // Les codes semés par défaut (RRF 96, FON 97, Salon Technique 98…) sont la raison
        // pour laquelle le préfixe « 9 » des exemples SVXLink a été écarté — cf. #144.
        DtmfCodeRanges.IsValidForSalon(code).Should().BeTrue();
    }

    #endregion

    #region Constants Consistency

    [Fact]
    public void Constants_ShouldBeConsistent()
    {
        DtmfCodeRanges.ModuleRangeMin.Should().Be(1);
        DtmfCodeRanges.ModuleRangeMax.Should().Be(19);
        DtmfCodeRanges.SalonRangeMin.Should().Be(20);
        DtmfCodeRanges.SalonRangeMax.Should().Be(9999);
        DtmfCodeRanges.AnnounceRangeMin.Should().Be(300);
        DtmfCodeRanges.AnnounceRangeMax.Should().Be(399);

        // Le préfixe talkgroup vit dans la plage d'annonces, déjà réservée : le réserver
        // n'enlève aux salons que les codes 35 et 3500-3599.
        DtmfCodeRanges.TalkGroupCommandPrefix.Should().Be("35");
        int.Parse(DtmfCodeRanges.TalkGroupCommandPrefix).Should().BeGreaterThan(0);

        // La plage salon commence juste après la plage module
        DtmfCodeRanges.SalonRangeMin.Should().Be(DtmfCodeRanges.ModuleRangeMax + 1);
    }

    #endregion
}
