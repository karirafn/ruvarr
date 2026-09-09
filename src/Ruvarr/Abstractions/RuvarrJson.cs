using System.Text.Json;

namespace Ruvarr.Abstractions;

internal static class RuvarrJson
{
    internal static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters = { new NfcStringConverter() }
    };
}
