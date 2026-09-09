using System.Text;
using System.Text.Json;

using Ruvarr.Abstractions;

using Shouldly;

namespace Ruvarr.UnitTests.Abstractions.NfcStringConverterTests;

public sealed class Read
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new NfcStringConverter() }
    };

    [Fact]
    public void WhenStringIsNfd_ReturnsNfcNormalized()
    {
        // Arrange
        // NFD: o + \u0308 (COMBINING DIAERESIS), a + \u0301 (COMBINING ACUTE ACCENT)
        // Written with explicit \u escapes so git/editor normalization cannot collapse them.
        string nfdTitle = "Skjaldb\u006f\u0308kustr\u0061\u0301kur";
        string json = $"{{\"value\":\"{nfdTitle}\"}}";
        string expectedNfc = "Skjaldb\u00f6kustr\u00e1kur";

        // Act
        TestRecord? result = JsonSerializer.Deserialize<TestRecord>(json, Options);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe(expectedNfc);
        result.Value.IsNormalized(NormalizationForm.FormC).ShouldBeTrue();
    }

    [Fact]
    public void WhenStringIsAlreadyNfc_ReturnsSameValue()
    {
        // Arrange
        // "Skjaldb\u00f6kustr\u00e1kur" — composed ö (U+00F6) and á (U+00E1), NFC by definition
        string nfcTitle = "Skjaldb\u00f6kustr\u00e1kur";
        string json = $"{{\"value\":\"{nfcTitle}\"}}";

        // Act
        TestRecord? result = JsonSerializer.Deserialize<TestRecord>(json, Options);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe(nfcTitle);
        result.Value.IsNormalized(NormalizationForm.FormC).ShouldBeTrue();
    }

    [Fact]
    public void WhenStringIsAscii_ReturnsSameValue()
    {
        // Arrange
        string json = "{\"value\":\"hello\"}";

        // Act
        TestRecord? result = JsonSerializer.Deserialize<TestRecord>(json, Options);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe("hello");
    }

    [Fact]
    public void WhenTokenIsNull_ReturnsNull()
    {
        // Arrange
        string json = "{\"value\":null}";

        // Act
        NullableRecord? result = JsonSerializer.Deserialize<NullableRecord>(json, Options);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBeNull();
    }

    private sealed record TestRecord(string Value);
    private sealed record NullableRecord(string? Value);
}
