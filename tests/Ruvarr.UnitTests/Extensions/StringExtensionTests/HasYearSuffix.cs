using Ruvarr.Extensions;

using Shouldly;

namespace Ruvarr.UnitTests.Extensions.StringExtensionTests;

public sealed class HasYearSuffix
{
    [Theory]
    [InlineData("Katla (2021)")]
    [InlineData("Trapped (2015)")]
    [InlineData("The Show (1999)")]
    [InlineData("Name (2021) ")]
    public void ReturnsTrue_WhenStringEndsWithYearInParentheses(string value)
    {
        // Arrange

        // Act
        bool result = value.HasYearSuffix();

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Katla")]
    [InlineData("Trapped")]
    [InlineData("The Show (documentary)")]
    [InlineData("Name (12)")]
    [InlineData("Name (20210)")]
    [InlineData("")]
    public void ReturnsFalse_WhenStringDoesNotEndWithYearInParentheses(string value)
    {
        // Arrange

        // Act
        bool result = value.HasYearSuffix();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsFalse_WhenStringIsNull()
    {
        // Arrange
        string? value = null;

        // Act
        bool result = value.HasYearSuffix();

        // Assert
        result.ShouldBeFalse();
    }
}
