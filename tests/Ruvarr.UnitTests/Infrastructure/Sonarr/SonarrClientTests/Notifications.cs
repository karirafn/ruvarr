using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Sonarr.SonarrClientTests;

public sealed class Notifications
{
    private static readonly SonarrNotification Notification = new()
    {
        Id = 7,
        Name = "Ruvarr",
        Implementation = "Webhook",
        ConfigContract = "WebhookSettings",
        Fields = [new SonarrNotificationField("url", JsonSerializer.SerializeToElement("http://10.0.0.5:8080/api/sonarr/webhook"))],
        OnDownload = true,
    };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "HttpClient disposal is not needed for an in-memory handler")]
    private static SonarrClient CreateSut(RecordingHttpMessageHandler handler) =>
        new(NullLogger<SonarrClient>.Instance, new HttpClient(handler) { BaseAddress = new Uri("http://sonarr.local") });

    [Fact]
    public async Task GetNotificationsAsync_ParsesSonarrResponse()
    {
        // Arrange
        using RecordingHttpMessageHandler handler = new(HttpStatusCode.OK, SonarrFixtures.Load(SonarrFixtures.NotificationList));
        SonarrClient sut = CreateSut(handler);

        // Act
        Result<IReadOnlyList<SonarrNotification>> result = await sut.GetNotificationsAsync(TestContext.Current.CancellationToken);

        // Assert
        handler.Path.ShouldBe("/api/v3/notification");
        result.IsSuccess.ShouldBeTrue();
        SonarrNotification notification = result.FromResult().ShouldHaveSingleItem();
        notification.Id.ShouldBe(1);
        notification.Name.ShouldBe("Ruvarr");
        notification.Implementation.ShouldBe("Webhook");
        notification.OnDownload.ShouldBeTrue();
        notification.OnGrab.ShouldBeFalse();
        notification.Fields.Single(f => f.Name == "url").Value!.Value.GetString()
            .ShouldBe("http://172.17.0.3:8080/api/sonarr/webhook");
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsFailure_WhenSonarrFails()
    {
        // Arrange
        using RecordingHttpMessageHandler handler = new(HttpStatusCode.Unauthorized);
        SonarrClient sut = CreateSut(handler);

        // Act
        Result<IReadOnlyList<SonarrNotification>> result = await sut.GetNotificationsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task TestNotificationAsync_PostsNotificationToTestEndpoint()
    {
        // Arrange
        using RecordingHttpMessageHandler handler = new(HttpStatusCode.OK);
        SonarrClient sut = CreateSut(handler);

        // Act
        RuvarrResult result = await sut.TestNotificationAsync(Notification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Path.ShouldBe("/api/v3/notification/test");
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        body.RootElement.GetProperty("implementation").GetString().ShouldBe("Webhook");
        body.RootElement.GetProperty("onDownload").GetBoolean().ShouldBeTrue();
        body.RootElement.GetProperty("fields")[0].GetProperty("value").GetString()
            .ShouldBe("http://10.0.0.5:8080/api/sonarr/webhook");
    }

    // Sonarr answers a failed webhook test with 500 when the callback refuses the connection,
    // and with 400 when it rejects the response; both mean the test did not pass.
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task TestNotificationAsync_ReturnsFailure_WhenSonarrRejectsTest(HttpStatusCode statusCode)
    {
        // Arrange
        using RecordingHttpMessageHandler handler = new(statusCode);
        SonarrClient sut = CreateSut(handler);

        // Act
        RuvarrResult result = await sut.TestNotificationAsync(Notification, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task TestNotificationAsync_ReturnsFailure_WhenSonarrIsUnreachable()
    {
        // Arrange
        using RecordingHttpMessageHandler handler = new(HttpStatusCode.OK) { ThrowOnSend = new HttpRequestException("refused") };
        SonarrClient sut = CreateSut(handler);

        // Act
        RuvarrResult result = await sut.TestNotificationAsync(Notification, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateNotificationAsync_PostsNotification()
    {
        // Arrange
        using RecordingHttpMessageHandler handler = new(HttpStatusCode.Created);
        SonarrClient sut = CreateSut(handler);

        // Act
        RuvarrResult result = await sut.CreateNotificationAsync(Notification with { Id = null }, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Path.ShouldBe("/api/v3/notification");
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        body.RootElement.TryGetProperty("id", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateNotificationAsync_ReturnsFailure_WhenSonarrRejects()
    {
        // Arrange
        using RecordingHttpMessageHandler handler = new(HttpStatusCode.BadRequest);
        SonarrClient sut = CreateSut(handler);

        // Act
        RuvarrResult result = await sut.CreateNotificationAsync(Notification, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateNotificationAsync_PutsNotificationById()
    {
        // Arrange
        using RecordingHttpMessageHandler handler = new(HttpStatusCode.Accepted);
        SonarrClient sut = CreateSut(handler);

        // Act
        RuvarrResult result = await sut.UpdateNotificationAsync(7, Notification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        handler.Method.ShouldBe(HttpMethod.Put);
        handler.Path.ShouldBe("/api/v3/notification/7");
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        body.RootElement.GetProperty("id").GetInt32().ShouldBe(7);
    }
}
