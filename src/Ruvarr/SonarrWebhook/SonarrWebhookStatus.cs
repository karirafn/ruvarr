namespace Ruvarr.SonarrWebhook;

internal enum SonarrWebhookStatus
{
    NotConfigured,
    Pending,
    Registered,
    SonarrUnreachable,
    CallbackUnreachable,
}
