using System.Text.Json;

using Ruvarr.Abstractions;

namespace Ruvarr.Infrastructure.Ruv;

// RÚV-specific JSON options: Web defaults + NFC normalisation + strict nullable enforcement.
// Kept in Infrastructure/Ruv/ rather than Abstractions/ because RespectNullableAnnotations
// is RÚV-specific — applying it globally would subject TVDB/Sonarr (unaudited) to the same
// strict boundary without the corresponding DTO trim. See ADR 0012.
internal static class RuvJson
{
    internal static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters = { new NfcStringConverter() },
        RespectNullableAnnotations = true
    };

    static RuvJson() => Default.MakeReadOnly(populateMissingResolver: true);
}
