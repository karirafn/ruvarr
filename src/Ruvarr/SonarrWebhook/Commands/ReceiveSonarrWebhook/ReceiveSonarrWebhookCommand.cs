namespace Ruvarr.SonarrWebhook.Commands.ReceiveSonarrWebhook;

// The subset of Sonarr's WebhookPayload Ruvarr reads. Only used to pick programs to re-sync;
// nothing in it is stored.
internal sealed record ReceiveSonarrWebhookCommand(string? EventType, SonarrWebhookSeries? Series);

internal sealed record SonarrWebhookSeries(int? TvdbId);
