using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Commands.DismissDownloadQueueItem;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Events;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.IntegrationTests.Downloads.Commands.DismissDownloadQueueItem;

public sealed class DismissDownloadQueueItemHandlerTests(IntegrationTestFactory factory)
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
    public async Task DeletesFailedItem()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ArrangeAsync("EP001", cancellationToken, episode => new DownloadQueueItemBuilder().WithEpisode(episode).Failed("network error").Build());

        // Act
        RuvarrResult result = await DismissAsync("EP001", cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await CountItemsAsync(cancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task DeletesExhaustedItem()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ArrangeAsync("EP002", cancellationToken, episode => new DownloadQueueItemBuilder().WithEpisode(episode).Exhausted().Build());

        // Act
        RuvarrResult result = await DismissAsync("EP002", cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await CountItemsAsync(cancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task RaisesDownloadDismissedEvent()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ArrangeAsync("EP003", cancellationToken, episode => new DownloadQueueItemBuilder().WithEpisode(episode).Failed("network error").Build());
        IDomainEventBroadcaster broadcaster = factory.Services.GetRequiredService<IDomainEventBroadcaster>();
        using CancellationTokenSource subscriptionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        subscriptionCts.CancelAfter(TimeSpan.FromSeconds(10));
        IAsyncEnumerator<DownloadDismissedEvent> events = broadcaster
            .Subscribe<DownloadDismissedEvent>(subscriptionCts.Token)
            .GetAsyncEnumerator(subscriptionCts.Token);
        ValueTask<bool> next = events.MoveNextAsync();

        // Act
        RuvarrResult result = await DismissAsync("EP003", cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await next).ShouldBeTrue();
        events.Current.Item.FailureReason.ShouldBe("network error");
        await events.DisposeAsync();
    }

    [Fact]
    public async Task DeletesOlderFailedItem_AndKeepsNewerCompleteItems()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ArrangeAsync(
            "EP004",
            cancellationToken,
            episode => new DownloadQueueItemBuilder().WithEpisode(episode).Failed("network error").Build(),
            episode => new DownloadQueueItemBuilder().WithEpisode(episode).Downloaded().Build(),
            episode => new DownloadQueueItemBuilder().WithEpisode(episode).Downloaded().Build());

        // Act
        RuvarrResult result = await DismissAsync("EP004", cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        List<DownloadQueueStatus> remaining = await StatusesAsync(cancellationToken);
        remaining.ShouldBe([DownloadQueueStatus.Complete, DownloadQueueStatus.Complete]);
    }

    [Theory]
    [InlineData(DownloadQueueStatus.Pending)]
    [InlineData(DownloadQueueStatus.Downloading)]
    public async Task ReturnsNotDismissable_AndKeepsRow_WhenItemIsUnfinishedButNotFailed(DownloadQueueStatus status)
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string episodeRuvId = $"EP-{status}";
        await ArrangeAsync(episodeRuvId, cancellationToken, episode =>
        {
            DownloadQueueItem item = DownloadQueueItem.Create(episode);
            if (status is DownloadQueueStatus.Downloading)
            {
                item.MarkDownloading();
            }

            return item;
        });

        // Act
        RuvarrResult result = await DismissAsync(episodeRuvId, cancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(DownloadErrors.ItemNotDismissableCode);
        (await StatusesAsync(cancellationToken)).ShouldBe([status]);
    }

    [Fact]
    public async Task ReturnsNotFound_WhenEpisodeHasOnlyCompleteItems()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ArrangeAsync("EP005", cancellationToken, episode => new DownloadQueueItemBuilder().WithEpisode(episode).Downloaded().Build());

        // Act
        RuvarrResult result = await DismissAsync("EP005", cancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(DownloadErrors.ItemNotFoundCode);
        (await CountItemsAsync(cancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task ReturnsNotFound_WhenItemDoesNotExist()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        RuvarrResult result = await DismissAsync("NONEXISTENT", cancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(DownloadErrors.ItemNotFoundCode);
    }

    private async Task ArrangeAsync(
        string episodeRuvId,
        CancellationToken cancellationToken,
        params Func<RuvEpisode, DownloadQueueItem>[] itemFactories)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "RUV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode(episodeRuvId, new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        foreach (Func<RuvEpisode, DownloadQueueItem> itemFactory in itemFactories)
        {
            dbContext.Set<DownloadQueueItem>().Add(itemFactory(episode));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<RuvarrResult> DismissAsync(string episodeRuvId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IRequestHandler<DismissDownloadQueueItemCommand> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<DismissDownloadQueueItemCommand>>();
        return await handler.Handle(new DismissDownloadQueueItemCommand(episodeRuvId), cancellationToken);
    }

    private async Task<int> CountItemsAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        return await dbContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
    }

    private async Task<List<DownloadQueueStatus>> StatusesAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        return await dbContext.Set<DownloadQueueItem>().Select(x => x.Status).ToListAsync(cancellationToken);
    }
}
