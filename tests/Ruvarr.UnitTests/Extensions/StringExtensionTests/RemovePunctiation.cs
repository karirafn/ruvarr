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
}