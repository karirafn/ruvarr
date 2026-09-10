using System.Text;

using Ruvarr.Extensions;

using Shouldly;

namespace Ruvarr.UnitTests.Extensions.StringExtensionTests;

public sealed class ToNfc
{
    [Fact]
    public void WhenStringIsNfd_ReturnsNfcNormalized()
    {
        // Arrange
        // NFD: o + \u0308 (COMBINING DIAERESIS), a + \u0301 (COMBINING ACUTE ACCENT)
        // Written with explicit \u escapes so git/editor normalization cannot collapse them.
        string nfdInput = "Skjaldb\u006f\u0308kustr\u0061\u0301kur";
        string expectedNfc = "Skjaldb\u00f6kustr\u00e1kur";

        // Act
        string result = nfdInput.ToNfc();

        // Assert
        nfdInput.IsNormalized(NormalizationForm.FormC).ShouldBeFalse();
        result.ShouldBe(expectedNfc);
        result.IsNormalized(NormalizationForm.FormC).ShouldBeTrue();
    }

    [Fact]
    public void WhenStringIsAlreadyNfc_ReturnsSameValue()
    {
        // Arrange
        // "Skjaldb\u00f6kustr\u00e1kur" — composed ö (U+00F6) and á (U+00E1), NFC by definition
        string nfcInput = "Skjaldb\u00f6kustr\u00e1kur";

        // Act
        string result = nfcInput.ToNfc();

        // Assert
        result.ShouldBe(nfcInput);
        result.IsNormalized(NormalizationForm.FormC).ShouldBeTrue();
    }

    [Fact]
    public void WhenStringIsAscii_ReturnsSameValue()
    {
        // Arrange
        string asciiInput = "hello world";

        // Act
        string result = asciiInput.ToNfc();

        // Assert
        result.ShouldBe(asciiInput);
    }
}
