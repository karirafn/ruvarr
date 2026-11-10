using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Sonarr.Models;

// Sonarr's NotificationResource (api/v3/notification). Field values are polymorphic
// (string, number, list) depending on the field type in the notification schema.
internal sealed record SonarrNotification
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Id { get; init; }

    public required string Name { get; init; }

    public required string Implementation { get; init; }

    public required string ConfigContract { get; init; }

    public required IReadOnlyList<SonarrNotificationField> Fields { get; init; }

    public IReadOnlyList<int> Tags { get; init; } = [];

    public bool OnGrab { get; init; }

    public bool OnDownload { get; init; }

    public bool OnUpgrade { get; init; }

    public bool OnImportComplete { get; init; }

    public bool OnRename { get; init; }

    public bool OnSeriesAdd { get; init; }

    public bool OnSeriesDelete { get; init; }

    public bool OnEpisodeFileDelete { get; init; }

    public bool OnEpisodeFileDeleteForUpgrade { get; init; }

    public bool OnHealthIssue { get; init; }

    public bool IncludeHealthWarnings { get; init; }

    public bool OnHealthRestored { get; init; }

    public bool OnApplicationUpdate { get; init; }

    public bool OnManualInteractionRequired { get; init; }
}

internal sealed record SonarrNotificationField(string Name, JsonElement? Value);
