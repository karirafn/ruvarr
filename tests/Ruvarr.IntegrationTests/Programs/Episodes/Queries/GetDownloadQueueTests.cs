using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Extensions;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Episodes.Queries;

public sealed class GetDownloadQueueTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    [Fact]
    public async Task DoesNotReturnOrphanedItems()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(2, "RÚV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("DEF456", new Uri("https://example.com/ep.mp4"), "Test Episode", "Description", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        dbContext.EnqueueDownload(episode);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Simulate an orphaned item by inserting a second queue item via raw SQL
        // (bypassing EF's FK update so the episode's download_queue_item_id still points to the first item)
        await dbContext.Database.ExecuteSqlAsync(
            $"INSERT INTO download_queue (created, status) VALUES ({DateTime.UtcNow.ToString("o")}, 'Pending')",
            cancellationToken);

        // Act
        HttpResponseMessage response = await factory.CreateClient()
            .GetAsync("/programs/download-queue?includeDownloaded=true", cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<DownloadQueueItemSummary>? items = await response.Content
            .ReadFromJsonAsync<List<DownloadQueueItemSummary>>(cancellationToken);
        items.ShouldNotBeNull();
        items.ShouldAllBe(x => x.EpisodeRuvId != null);
    }
}
