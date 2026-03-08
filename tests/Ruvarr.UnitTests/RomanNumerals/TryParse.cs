using Ruvarr.RomanNumerals;

using Shouldly;

namespace Ruvarr.UnitTests.RomanNumerals;

public sealed class TryParse
{
    [Theory]
    [InlineData("I")]
    [InlineData("II")]
    [InlineData("III")]
    [InlineData("IV")]
    [InlineData("V")]
    [InlineData("VI")]
    [InlineData("VII")]
    [InlineData("VIII")]
    [InlineData("IX")]
    [InlineData("X")]
    [InlineData("XIV")]
    [InlineData("XIX")]
    [InlineData("XX")]
    [InlineData("XL")]
    [InlineData("L")]
    [InlineData("XC")]
    [InlineData("XCIX")]
    [InlineData("C")]
    [InlineData("CD")]
    [InlineData("D")]
    [InlineData("CM")]
    [InlineData("M")]
    [InlineData("MMXXVI")]
    public void ReturnsTrue_WhenInputIsValid(string input)
    {
        // Arrange

        // Act
        bool result = RomanNumeral.TryParse(input, out _);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("IS")]
    [InlineData("IIII")]
    [InlineData("VV")]
    [InlineData("LL")]
    [InlineData("DD")]
    public void ReturnsFalse_WhenInputIsInvalid(string input)
    {
        // Arrange

        // Act
        bool result = RomanNumeral.TryParse(input, out _);

        // Assert
        result.ShouldBeFalse();
    }
}