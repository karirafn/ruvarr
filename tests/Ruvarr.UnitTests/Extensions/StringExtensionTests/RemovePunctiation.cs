using Ruvarr.Extensions;

using Shouldly;

namespace Ruvarr.UnitTests.Extensions.StringExtensionTests;

public sealed class RemovePunctiation
{
    [Theory]
    [InlineData("Hello, World! This is a test. (With punctuation)", "Hello World This is a test With punctuation")]
    [InlineData("Chicago P.D.", "Chicago PD")]
    [InlineData("Gettu betur í 40 ár", "Gettu betur í 40 ár")]
    public void RemovesPunctuation(string input, string expected)
    {
        // Arrange

        // Act
        string result = input.Sanitized();

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void WhenInputIsNfd_NormalizesToNfc()
    {
        // Arrange
        // NFD: o + \u0308 (combining diaeresis) gives ö; a + \u0301 (combining acute) gives á
        // C# \uXXXX escapes are used so no raw combining bytes appear in source.
        string nfdInput = "Skjaldbo\u0308kustra\u0301kur";
        string expectedNfc = "Skjaldbökustrákur";

        // Act
        string result = nfdInput.Sanitized();

        // Assert
        result.ShouldBe(expectedNfc);
    }
}
