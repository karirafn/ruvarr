using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.IntegrationTests.Downloads;

public sealed class EpisodeDownloadRequestedEventHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
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
    public async Task CreatesDownloadQueueItem_WhenDownloadRequested()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ArrangeAsync("ABC123", cancellationToken);

        // Act
        await RequestDownloadAsync("ABC123", cancellationToken);

        // Assert
        List<DownloadQueueItem> items = await ItemsAsync(cancellationToken);
        items.ShouldHaveSingleItem().Status.ShouldBe(DownloadQueueStatus.Pending);
    }

    [Fact]
    public async Task CreatesNewPendingItem_WhenAllExistingItemsAreComplete()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ArrangeAsync(
            "EP001",
            cancellationToken,
            episode => new DownloadQueueItemBuilder().WithEpisode(episode).Downloaded().Build(),
            episode => new DownloadQueueItemBuilder().WithEpisode(episode).Downloaded().Build());

        // Act
        await RequestDownloadAsync("EP001", cancellationToken);

        // Assert
        List<DownloadQueueItem> items = await ItemsAsync(cancellationToken);
        items.Select(x => x.Status).ShouldBe(
            [DownloadQueueStatus.Complete, DownloadQueueStatus.Complete, DownloadQueueStatus.Pending]);
    }

    [Theory]
    [InlineData(DownloadQueueStatus.Pending)]
    [InlineData(DownloadQueueStatus.Downloading)]
    public async Task DoesNotCreateItem_WhenEpisodeHasActiveItem(DownloadQueueStatus status)
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ArrangeAsync($"EP-{status}", cancellationToken, episode =>
        {
            DownloadQueueItem item = DownloadQueueItem.Create(episode);
            if (status is DownloadQueueStatus.Downloading)
            {
                item.MarkDownloading();
            }

            return item;
        });

        // Act
        await RequestDownloadAsync($"EP-{status}", cancellationToken);

        // Assert
        List<DownloadQueueItem> items = await ItemsAsync(cancellationToken);
        items.ShouldHaveSingleItem().Status.ShouldBe(status);
    }

    [Fact]
    public async Task RetriesExistingFailedItem_KeepingRetryCount_WithoutCreatingItem()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ArrangeAsync(
            "EP002",
            cancellationToken,
            episode => new DownloadQueueItemBuilder().WithEpisode(episode).Downloaded().Build(),
            episode => new DownloadQueueItemBuilder().WithEpisode(episode).Failed("network error").Build());

        // Act
        await RequestDownloadAsync("EP002", cancellationToken);

        // Assert
        List<DownloadQueueItem> items = await ItemsAsync(cancellationToken);
        items.Count.ShouldBe(2);
        DownloadQueueItem retried = items[1];
        retried.ShouldSatisfyAllConditions(
            () => retried.Status.ShouldBe(DownloadQueueStatus.Pending),
            () => retried.FailureReason.ShouldBeNull(),
            () => retried.RetryCount.ShouldBe(1));
    }

    [Fact]
    public async Task RetriesExistingExhaustedItem_KeepingRetryCount_WithoutCreatingItem()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ArrangeAsync(
            "EP003",
            cancellationToken,
            episode => new DownloadQueueItemBuilder().WithEpisode(episode).Exhausted().Build());

        // Act
        await RequestDownloadAsync("EP003", cancellationToken);

        // Assert
        List<DownloadQueueItem> items = await ItemsAsync(cancellationToken);
        DownloadQueueItem retried = items.ShouldHaveSingleItem();
        retried.ShouldSatisfyAllConditions(
            () => retried.Status.ShouldBe(DownloadQueueStatus.Pending),
            () => retried.RetryCount.ShouldBe(RetrySchedule.MaxRetries + 1));
    }

    private async Task ArrangeAsync(
        string episodeRuvId,
        CancellationToken cancellationToken,
        params Func<RuvEpisode, DownloadQueueItem>[] itemFactories)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "RÚV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode(episodeRuvId, new Uri("https://example.com/ep.mp4"), "Test Episode", "Description", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        foreach (Func<RuvEpisode, DownloadQueueItem> itemFactory in itemFactories)
        {
            dbContext.Set<DownloadQueueItem>().Add(itemFactory(episode));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RequestDownloadAsync(string episodeRuvId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        RuvEpisode episode = await dbContext.Set<RuvEpisode>()
            .Where(x => x.RuvId == episodeRuvId)
            .FirstAsync(cancellationToken);

        episode.RequestDownload();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<DownloadQueueItem>> ItemsAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        return await dbContext.Set<DownloadQueueItem>()
            .OrderBy(x => x.Created)
            .ToListAsync(cancellationToken);
    }
}
