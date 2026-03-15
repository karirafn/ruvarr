using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Extensions;
using Ruvarr.Downloads.Queries.GetDownloadQueue;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Episodes.Queries;

public sealed class GetDownloadQueueHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task DoesNotReturnOrphanedItems()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>>>();

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
        List<DownloadQueueItemSummary> items = await handler.Handle(
            new GetDownloadQueueQuery(IncludeDownloaded: true),
            cancellationToken);

        // Assert
        items.ShouldAllBe(x => x.EpisodeRuvId != null);
    }
}
