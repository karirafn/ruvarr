using Ruvarr.Extensions;

using Shouldly;

namespace Ruvarr.UnitTests.Extensions.StringExtensionTests;

public sealed class WithoutYearSuffix
{
    [Theory]
    [InlineData("Katla (2021)", "Katla")]
    [InlineData("Trapped (2015)", "Trapped")]
    [InlineData("The Show (1999)", "The Show")]
    [InlineData("Name (2021) ", "Name")]
    public void ReturnsTrimmedBaseName_WhenStringEndsWithYearInParentheses(string value, string expected)
    {
        // Arrange

        // Act
        string? result = value.WithoutYearSuffix();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Katla")]
    [InlineData("The Show (documentary)")]
    [InlineData("")]
    public void ReturnsOriginalValue_WhenStringDoesNotEndWithYearInParentheses(string value)
    {
        // Arrange

        // Act
        string? result = value.WithoutYearSuffix();

        // Assert
        result.ShouldBe(value);
    }

    [Fact]
    public void ReturnsNull_WhenStringIsNull()
    {
        // Arrange
        string? value = null;

        // Act
        string? result = value.WithoutYearSuffix();

        // Assert
        result.ShouldBeNull();
    }
}
