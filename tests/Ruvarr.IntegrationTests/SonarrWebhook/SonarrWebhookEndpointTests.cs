using System.Net.Http.Json;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.IntegrationTests.SonarrWebhook;

public sealed class SonarrWebhookEndpointTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private const string Path = "/api/sonarr/webhook";

    // The body Sonarr v4 POSTs when it tests a webhook (captured from Sonarr 4.0.20).
    private const string SonarrTestPayload = """
        {
          "series": { "id": 1, "title": "Test Title", "path": "C:\\testpath", "tvdbId": 1234, "tvMazeId": 0, "tmdbId": 0, "type": "standard", "year": 0, "tags": ["test-tag"] },
          "episodes": [ { "id": 123, "episodeNumber": 1, "seasonNumber": 1, "title": "Test title", "seriesId": 0, "tvdbId": 0 } ],
          "eventType": "Test",
          "instanceName": "Sonarr",
          "applicationUrl": ""
        }
        """;

    public async ValueTask InitializeAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task AcceptsSonarrTestDelivery()
    {
        // Arrange
        using HttpClient client = factory.CreateClient();
        using StringContent content = new(SonarrTestPayload, Encoding.UTF8, "application/json");

        // Act
        using HttpResponseMessage response = await client.PostAsync(Path, content, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task DownloadDeliveryQueuesProgramsOfTheSeriesForSync()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
            RuvProgram program = new RuvProgramBuilder().WithRuvId(44101).WithName("Webhook Program").WithMultipleEpisodes().Build();
            program.MatchTvdb(new TvdbSeriesBuilder().WithId(441001).Build());
            dbContext.Set<RuvProgram>().Add(program);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            Path,
            new { eventType = "Download", series = new { tvdbId = 441001 } },
            cancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        factory.Services.GetRequiredService<ProgramRefreshNotifier>().Items.ShouldContain(i => i.RuvId == 44101);
    }

    [Fact]
    public async Task AcceptsDeliveryForUnknownSeries()
    {
        // Arrange
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            Path,
            new { eventType = "Download", series = new { tvdbId = 999_999_999 } },
            TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task AcceptsDeliveryWithoutSeries()
    {
        // Arrange
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            Path,
            new { eventType = "HealthRestored" },
            TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task RejectsGet()
    {
        // Arrange
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
    }
}
