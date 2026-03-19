using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
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
    public async Task ReturnsPendingItemsSortedOldestFirst()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>>>();

        RuvProgram programA = RuvProgram.Create(2, "RÚV1", "Program A", null, multipleEpisodes: true);
        RuvProgram programB = RuvProgram.Create(3, "RÚV1", "Program B", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(programA);
        dbContext.Set<RuvProgram>().Add(programB);
        await dbContext.SaveChangesAsync(cancellationToken);

        programA.TryAddEpisode("EP001", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        programB.TryAddEpisode("EP002", new Uri("https://example.com/ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        DateTime olderCreated = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime newerCreated = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        RuvEpisode episodeOlder = programA.Episodes[0];
        RuvEpisode episodeNewer = programB.Episodes[0];
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episodeOlder));
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episodeNewer));
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"UPDATE download_queue SET created = {olderCreated.ToString("o")} WHERE id = (SELECT download_queue_item_id FROM episodes WHERE ruv_id = 'EP001')",
            cancellationToken);
        await dbContext.Database.ExecuteSqlAsync(
            $"UPDATE download_queue SET created = {newerCreated.ToString("o")} WHERE id = (SELECT download_queue_item_id FROM episodes WHERE ruv_id = 'EP002')",
            cancellationToken);

        // Act
        List<DownloadQueueItemSummary> items = await handler.Handle(
            new GetDownloadQueueQuery(IncludeDownloaded: false),
            cancellationToken);

        // Assert
        items.Count.ShouldBe(2);
        items[0].EpisodeRuvId.ShouldBe("EP001");
        items[1].EpisodeRuvId.ShouldBe("EP002");
    }

    [Fact]
    public async Task ReturnsDownloadingItemBeforePendingItems()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>>>();

        RuvProgram programA = RuvProgram.Create(4, "RÚV1", "Program C", null, multipleEpisodes: true);
        RuvProgram programB = RuvProgram.Create(5, "RÚV1", "Program D", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(programA);
        dbContext.Set<RuvProgram>().Add(programB);
        await dbContext.SaveChangesAsync(cancellationToken);

        programA.TryAddEpisode("EP003", new Uri("https://example.com/ep3.mp4"), "Episode 3", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        programB.TryAddEpisode("EP004", new Uri("https://example.com/ep4.mp4"), "Episode 4", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episodePending = programA.Episodes[0];
        RuvEpisode episodeDownloading = programB.Episodes[0];
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episodePending));
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episodeDownloading));
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"UPDATE download_queue SET status = 'Downloading' WHERE id = (SELECT download_queue_item_id FROM episodes WHERE ruv_id = 'EP004')",
            cancellationToken);

        // Act
        List<DownloadQueueItemSummary> items = await handler.Handle(
            new GetDownloadQueueQuery(IncludeDownloaded: false),
            cancellationToken);

        // Assert
        items.Count.ShouldBe(2);
        items[0].EpisodeRuvId.ShouldBe("EP004");
        items[1].EpisodeRuvId.ShouldBe("EP003");
    }

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

        program.TryAddEpisode("DEF456", new Uri("https://example.com/ep.mp4"), "Test Episode", "Description", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
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
