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
    public void IsValidForSalon_WithInvalidSalonCode_ShouldReturnFalse(int code)
    {
        DtmfCodeRanges.IsValidForSalon(code).Should().BeFalse();
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

        // La plage salon commence juste après la plage module
        DtmfCodeRanges.SalonRangeMin.Should().Be(DtmfCodeRanges.ModuleRangeMax + 1);
    }

    #endregion
}
