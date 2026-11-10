using System.Reflection;
using System.Text;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Sonarr;

// Captured from a real Sonarr v4.0.20 (lscr.io/linuxserver/sonarr): GET api/v3/notification/schema
// (Webhook entry), GET api/v3/notification after creating the Ruvarr webhook, and the body Sonarr
// POSTs to a webhook on "Test".
internal static class SonarrFixtures
{
    public const string NotificationSchemaWebhook = "notification-schema-webhook.json";
    public const string NotificationList = "notification-list.json";
    public const string WebhookTestPayload = "webhook-test-payload.json";

    public static string Load(string filename)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"Ruvarr.UnitTests.Infrastructure.Sonarr.Fixtures.{filename}";
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        stream.ShouldNotBeNull($"Embedded resource '{resourceName}' not found in assembly.");
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
