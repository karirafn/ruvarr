using System.Text.Json;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.SonarrWebhook;
using Ruvarr.UnitTests.Infrastructure.Sonarr;

using Shouldly;

namespace Ruvarr.UnitTests.SonarrWebhook;

public sealed class RuvarrWebhookNotificationTests
{
    private static readonly Uri CallbackUrl = new("http://172.17.0.3:8080/api/sonarr/webhook");

    private static readonly string[] EnabledTriggers =
        ["onDownload", "onUpgrade", "onEpisodeFileDelete", "onEpisodeFileDeleteForUpgrade", "onRename"];

    private static JsonElement WebhookSchema()
    {
        using JsonDocument schema = JsonDocument.Parse(SonarrFixtures.Load(SonarrFixtures.NotificationSchemaWebhook));
        return schema.RootElement.EnumerateArray()
            .Single(x => x.GetProperty("implementation").GetString() == "Webhook")
            .Clone();
    }

    private static JsonElement Serialize(SonarrNotification notification) =>
        JsonSerializer.SerializeToElement(notification, RuvarrJson.Default);

    [Fact]
    public void For_UsesImplementationAndConfigContractFromSonarrSchema()
    {
        // Arrange
        JsonElement schema = WebhookSchema();

        // Act
        SonarrNotification notification = RuvarrWebhookNotification.For(CallbackUrl);

        // Assert
        notification.Name.ShouldBe("Ruvarr");
        notification.Implementation.ShouldBe(schema.GetProperty("implementation").GetString());
        notification.ConfigContract.ShouldBe(schema.GetProperty("configContract").GetString());
    }

    [Fact]
    public void For_SendsOnlyFieldsDeclaredBySonarrSchema()
    {
        // Arrange
        HashSet<string?> schemaFields = [.. WebhookSchema().GetProperty("fields").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())];

        // Act
        SonarrNotification notification = RuvarrWebhookNotification.For(CallbackUrl);

        // Assert
        notification.Fields.ShouldAllBe(f => schemaFields.Contains(f.Name));
    }

    [Fact]
    public void For_SetsUrlFieldToCallbackUrl()
    {
        // Act
        SonarrNotification notification = RuvarrWebhookNotification.For(CallbackUrl);

        // Assert
        notification.Fields.Single(f => f.Name == "url").Value!.Value.GetString().ShouldBe(CallbackUrl.AbsoluteUri);
    }

    [Fact]
    public void For_SetsMethodFieldToTheSchemaValueForPost()
    {
        // Arrange
        int postValue = WebhookSchema().GetProperty("fields").EnumerateArray()
            .Single(f => f.GetProperty("name").GetString() == "method")
            .GetProperty("selectOptions").EnumerateArray()
            .Single(o => o.GetProperty("name").GetString() == "POST")
            .GetProperty("value").GetInt32();

        // Act
        SonarrNotification notification = RuvarrWebhookNotification.For(CallbackUrl);

        // Assert
        notification.Fields.Single(f => f.Name == "method").Value!.Value.GetInt32().ShouldBe(postValue);
    }

    [Fact]
    public void For_SendsEveryTriggerFlagInSonarrSchema()
    {
        // Arrange
        string[] schemaTriggers = [.. WebhookSchema().EnumerateObject()
            .Where(p => p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            .Where(p => !p.Name.StartsWith("supports", StringComparison.Ordinal))
            .Select(p => p.Name)];

        // Act
        JsonElement json = Serialize(RuvarrWebhookNotification.For(CallbackUrl));

        // Assert
        schemaTriggers.ShouldNotBeEmpty();
        schemaTriggers.Where(name => !json.TryGetProperty(name, out JsonElement _)).ShouldBeEmpty();
    }

    [Fact]
    public void For_EnablesOnlyFileTriggers()
    {
        // Act
        JsonElement json = Serialize(RuvarrWebhookNotification.For(CallbackUrl));

        // Assert
        foreach (JsonProperty property in json.EnumerateObject().Where(p => p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            property.Value.GetBoolean().ShouldBe(EnabledTriggers.Contains(property.Name), property.Name);
        }
    }

    [Fact]
    public void IsRegisteredAt_IsTrue_ForNotificationSonarrStoredFromOurDefinition()
    {
        // Arrange
        SonarrNotification stored = StoredNotification();

        // Act
        bool result = RuvarrWebhookNotification.IsRegisteredAt(stored, CallbackUrl);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsRegisteredAt_IsFalse_WhenUrlDiffers()
    {
        // Arrange
        SonarrNotification stored = StoredNotification();

        // Act
        bool result = RuvarrWebhookNotification.IsRegisteredAt(stored, new Uri("http://10.0.0.9:8080/api/sonarr/webhook"));

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsRegisteredAt_IsFalse_WhenATriggerDiffers()
    {
        // Arrange
        SonarrNotification stored = StoredNotification() with { OnGrab = true };

        // Act
        bool result = RuvarrWebhookNotification.IsRegisteredAt(stored, CallbackUrl);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsRegisteredAt_IsFalse_WhenMethodIsNotPost()
    {
        // Arrange
        SonarrNotification stored = StoredNotification();
        stored = stored with
        {
            Fields = [.. stored.Fields.Select(f => f.Name == "method" ? f with { Value = JsonSerializer.SerializeToElement(2) } : f)],
        };

        // Act
        bool result = RuvarrWebhookNotification.IsRegisteredAt(stored, CallbackUrl);

        // Assert
        result.ShouldBeFalse();
    }

    private static SonarrNotification StoredNotification() =>
        JsonSerializer.Deserialize<IReadOnlyList<SonarrNotification>>(
            SonarrFixtures.Load(SonarrFixtures.NotificationList), RuvarrJson.Default)!.Single();
}
