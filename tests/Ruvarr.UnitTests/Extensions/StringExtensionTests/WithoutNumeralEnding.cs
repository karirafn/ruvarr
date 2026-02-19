using Ruvarr.Extensions;

using Shouldly;

namespace Ruvarr.UnitTests.Extensions.StringExtensionTests;

public class WithoutNumeralEnding
{
    [Fact]
    public void ReturnsOriginalValue_WhenValueDoesNotEndWithNumber()
    {
        // Arrange
        string value = "Series";

        // Act
        string? result = value.WithoutNumeralEnding();

        // Assert
        result.ShouldBe("Series");
    }

    [Theory]
    [InlineData("Blæja III", "Blæja")]
    [InlineData("Flækjur 4", "Flækjur")]
    public void ReturnsTrimmedValue_WhenValueEndsWithNumber(string value, string expected)
    {
        // Arrange

        // Act
        string? result = value.WithoutNumeralEnding();

        // Assert
        result.ShouldBe(expected);
    }
}