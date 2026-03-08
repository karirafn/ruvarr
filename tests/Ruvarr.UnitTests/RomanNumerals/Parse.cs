using Ruvarr.RomanNumerals;

using Shouldly;

namespace Ruvarr.UnitTests.RomanNumerals;

public sealed class Parse
{
    [Theory]
    [InlineData("I", 1)]
    [InlineData("II", 2)]
    [InlineData("III", 3)]
    [InlineData("IV", 4)]
    [InlineData("V", 5)]
    [InlineData("VI", 6)]
    [InlineData("VII", 7)]
    [InlineData("VIII", 8)]
    [InlineData("IX", 9)]
    [InlineData("X", 10)]
    [InlineData("XIV", 14)]
    [InlineData("XIX", 19)]
    [InlineData("XX", 20)]
    [InlineData("XL", 40)]
    [InlineData("L", 50)]
    [InlineData("XC", 90)]
    [InlineData("XCIX", 99)]
    [InlineData("C", 100)]
    [InlineData("CD", 400)]
    [InlineData("D", 500)]
    [InlineData("CM", 900)]
    [InlineData("M", 1000)]
    [InlineData("MMXXVI", 2026)]
    public void ParsesNumeral(string input, int expected)
    {
        // Arrange

        // Act
        RomanNumeral result = RomanNumeral.Parse(input);

        // Assert
        result.Number.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("IS")]
    [InlineData("IIII")]
    [InlineData("VV")]
    [InlineData("LL")]
    [InlineData("DD")]
    public void ThrowsException_WhenInputIsInvalid(string input)
    {
        // Arrange

        // Act
        Action action = () => RomanNumeral.Parse(input);

        // Assert
        action.ShouldThrow<InvalidRomanNumeralException>();
    }
}