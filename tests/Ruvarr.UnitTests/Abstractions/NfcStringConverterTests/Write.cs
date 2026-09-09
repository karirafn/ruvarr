using System.Text.Json;

using Ruvarr.Abstractions;

using Shouldly;

namespace Ruvarr.UnitTests.Abstractions.NfcStringConverterTests;

public sealed class Write
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new NfcStringConverter() }
    };

    [Fact]
    public void WhenSerializing_WritesValueUnchanged()
    {
        // Arrange -- NFD form: "e" + U+0301 (combining acute accent).
        // \uXXXX escapes guarantee no raw combining bytes appear in source.
        // Write must emit the string unchanged; normalization is Read's job, not Write's.
        string nfdValue = "e\u0301";
        TestRecord record = new(nfdValue);

        // Act
        string json = JsonSerializer.Serialize(record, Options);

        // Assert -- the serialized JSON contains the NFD codepoints (JSON-escaped as \u0301)
        // rather than the NFC precomposed codepoint (\u00E9), proving Write did not normalize.
        json.ShouldContain("e\\u0301");
        json.ShouldNotContain("\\u00e9");
    }

    private sealed record TestRecord(string Value);
}
