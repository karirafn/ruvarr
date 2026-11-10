using System.Text.Json;

using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.SonarrWebhook;

// The Webhook entry Ruvarr keeps registered in Sonarr's Connect settings. Names and value
// encodings are pinned against GET api/v3/notification/schema from a real Sonarr v4.
internal static class RuvarrWebhookNotification
{
    internal const string Name = "Ruvarr";

    private const string Implementation = "Webhook";
    private const string ConfigContract = "WebhookSettings";
    private const string UrlField = "url";
    private const string MethodField = "method";
    private const int PostMethod = 1;

    internal static SonarrNotification For(Uri callbackUrl)
    {
        ArgumentNullException.ThrowIfNull(callbackUrl);

        return new SonarrNotification
        {
            Name = Name,
            Implementation = Implementation,
            ConfigContract = ConfigContract,
            Fields =
            [
                new SonarrNotificationField(UrlField, JsonSerializer.SerializeToElement(callbackUrl.AbsoluteUri)),
                new SonarrNotificationField(MethodField, JsonSerializer.SerializeToElement(PostMethod)),
            ],
            OnDownload = true,
            OnUpgrade = true,
            OnEpisodeFileDelete = true,
            OnEpisodeFileDeleteForUpgrade = true,
            OnRename = true,
        };
    }

    internal static bool IsRuvarr(SonarrNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return notification.Name == Name;
    }

    internal static bool IsRegisteredAt(SonarrNotification notification, Uri callbackUrl)
    {
        ArgumentNullException.ThrowIfNull(notification);

        SonarrNotification expected = For(callbackUrl);

        // Fields and tags are compared separately; record equality covers name, contract and every trigger flag.
        bool sameDefinition = notification with { Id = null, Fields = expected.Fields, Tags = expected.Tags } == expected;

        return sameDefinition
            && FieldValue(notification, UrlField) is { ValueKind: JsonValueKind.String } url
            && url.GetString() == callbackUrl.AbsoluteUri
            && FieldValue(notification, MethodField) is { ValueKind: JsonValueKind.Number } method
            && method.TryGetInt32(out int methodValue)
            && methodValue == PostMethod;
    }

    private static JsonElement? FieldValue(SonarrNotification notification, string name) =>
        notification.Fields.FirstOrDefault(f => f.Name == name)?.Value;
}
