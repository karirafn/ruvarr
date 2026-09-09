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
        // Arrange
        string value = "hello";
        TestRecord record = new(value);

        // Act
        string json = JsonSerializer.Serialize(record, Options);
        TestRecord? deserialized = JsonSerializer.Deserialize<TestRecord>(json, Options);

        // Assert — Write is a pass-through: round-trip preserves the original value
        deserialized.ShouldNotBeNull();
        deserialized.Value.ShouldBe(value);
    }

    private sealed record TestRecord(string Value);
}
