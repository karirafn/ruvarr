using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Downloads;

public sealed class DownloadQueueItemPersistenceTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
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
    public async Task WhenFailedItemSaved_RetryFieldsRoundTrip()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(900, "RÚV1", "Persistence Test Program", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("PERSIST01", new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc",
            DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await arrangeContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        DownloadQueueItem item = DownloadQueueItem.Create(episode);
        item.MarkDownloading();
        item.MarkFailed("FFmpeg download failed");

        arrangeContext.Set<DownloadQueueItem>().Add(item);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        // Act
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        DownloadQueueItem saved = await verifyContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Failed)
            .FirstAsync(cancellationToken);

        // Assert
        saved.ShouldSatisfyAllConditions(
            () => saved.Status.ShouldBe(DownloadQueueStatus.Failed),
            () => saved.RetryCount.ShouldBe(1),
            () => saved.FailureReason.ShouldBe("FFmpeg download failed"),
            () => saved.NextRetryAt.ShouldNotBeNull());
    }

    [Fact]
    public async Task WhenExhaustedItemSaved_StatusAndRetryCountRoundTrip()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(901, "RÚV1", "Persistence Test Program 2", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("PERSIST02", new Uri("https://example.com/ep.mp4"), "Episode 2", "Desc",
            DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await arrangeContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        DownloadQueueItem item = DownloadQueueItem.Create(episode);
        item.MarkDownloading();

        // Exhaust all retries (MaxRetries + 1 calls to MarkFailed)
        for (int i = 0; i <= RetrySchedule.MaxRetries; i++)
        {
            item.MarkFailed("persistent failure");
        }

        arrangeContext.Set<DownloadQueueItem>().Add(item);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        // Act
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        DownloadQueueItem saved = await verifyContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Exhausted)
            .FirstAsync(cancellationToken);

        // Assert
        saved.ShouldSatisfyAllConditions(
            () => saved.Status.ShouldBe(DownloadQueueStatus.Exhausted),
            () => saved.RetryCount.ShouldBe(RetrySchedule.MaxRetries + 1),
            () => saved.NextRetryAt.ShouldBeNull());
    }
}
