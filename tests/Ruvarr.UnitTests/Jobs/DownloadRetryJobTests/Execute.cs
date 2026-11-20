using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Events;
using Ruvarr.Jobs;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.DownloadRetryJobTests;

public sealed class Execute
{
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public Execute()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _context.CancellationToken.Returns(CancellationToken.None);
    }

    private RuvarrDbContext CreateDbContext() => CreateDbContext(Guid.NewGuid().ToString());

    private RuvarrDbContext CreateDbContext(string databaseName) => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options,
        _serviceProvider);

    private static DownloadRetryJob CreateJob(RuvarrDbContext dbContext) =>
        new(NullLogger<DownloadRetryJob>.Instance, dbContext);

    [Fact]
    public async Task WhenFailedItemIsDue_TransitionsToPending()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        DownloadQueueItem item = new DownloadQueueItemBuilder()
            .Failed("ffmpeg download failed")
            .Build();

        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Simulate time passing: backdate next_retry_at so the item is now due
        dbContext.Entry(item).Property(x => x.NextRetryAt).CurrentValue = DateTime.UtcNow.AddHours(-1);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadRetryJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        DownloadQueueItem updated = await dbContext.Set<DownloadQueueItem>()
            .FirstAsync(TestContext.Current.CancellationToken);
        updated.Status.ShouldBe(DownloadQueueStatus.Pending);
        updated.NextRetryAt.ShouldBeNull();
    }

    [Fact]
    public async Task WhenFailedItemIsNotYetDue_LeavesItemUntouched()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        DownloadQueueItem item = new DownloadQueueItemBuilder()
            .Failed("ffmpeg download failed")
            .Build();

        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadRetryJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        DownloadQueueItem updated = await dbContext.Set<DownloadQueueItem>()
            .FirstAsync(TestContext.Current.CancellationToken);
        updated.Status.ShouldBe(DownloadQueueStatus.Failed);
        updated.NextRetryAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task WhenExhaustedItem_LeavesItemUntouched()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        DownloadQueueItem item = new DownloadQueueItemBuilder()
            .Exhausted()
            .Build();

        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadRetryJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        DownloadQueueItem updated = await dbContext.Set<DownloadQueueItem>()
            .FirstAsync(TestContext.Current.CancellationToken);
        updated.Status.ShouldBe(DownloadQueueStatus.Exhausted);
    }

    private sealed class RecordingHandler<TEvent> : IDomainEventHandler<TEvent> where TEvent : IDomainEvent
    {
        public List<TEvent> Handled { get; } = [];

        public Task Handle(TEvent @event, CancellationToken cancellationToken)
        {
            Handled.Add(@event);
            return Task.CompletedTask;
        }
    }

    private RecordingHandler<TEvent> RecordEvents<TEvent>() where TEvent : IDomainEvent
    {
        RecordingHandler<TEvent> handler = new();
        _serviceProvider.GetService(typeof(IEnumerable<IDomainEventHandler<TEvent>>))
            .Returns(new IDomainEventHandler<TEvent>[] { handler });
        return handler;
    }

    private async Task<string> SeedAsync(DownloadQueueItem item, bool makeDue = false)
    {
        string databaseName = Guid.NewGuid().ToString();
        using RuvarrDbContext seedContext = CreateDbContext(databaseName);
        seedContext.Set<DownloadQueueItem>().Add(item);
        await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (makeDue)
        {
            seedContext.Entry(item).Property(x => x.NextRetryAt).CurrentValue = DateTime.UtcNow.AddHours(-1);
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return databaseName;
    }

    private static RuvEpisode DownloadedEpisode() =>
        new RuvEpisodeBuilder().BuildMatchedWithStatus(SonarrEpisodeStatus.Downloaded);

    [Fact]
    public async Task WhenFailedItemIsSuperseded_RemovesItem()
    {
        // Arrange
        string databaseName = await SeedAsync(new DownloadQueueItemBuilder()
            .WithEpisode(DownloadedEpisode())
            .Failed("ffmpeg download failed")
            .Build());
        using RuvarrDbContext dbContext = CreateDbContext(databaseName);

        // Act
        await CreateJob(dbContext).Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        using RuvarrDbContext verifyContext = CreateDbContext(databaseName);
        (await verifyContext.Set<DownloadQueueItem>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task WhenExhaustedItemIsSuperseded_RaisesDownloadDismissedEvent()
    {
        // Arrange
        RecordingHandler<DownloadDismissedEvent> dismissed = RecordEvents<DownloadDismissedEvent>();
        string databaseName = await SeedAsync(new DownloadQueueItemBuilder()
            .WithEpisode(DownloadedEpisode())
            .Exhausted()
            .Build());
        using RuvarrDbContext dbContext = CreateDbContext(databaseName);

        // Act
        await CreateJob(dbContext).Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        dismissed.Handled.ShouldHaveSingleItem();
        using RuvarrDbContext verifyContext = CreateDbContext(databaseName);
        (await verifyContext.Set<DownloadQueueItem>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task WhenDueFailedItemIsSuperseded_DismissesInsteadOfRequeueing()
    {
        // Arrange
        RecordingHandler<DownloadDismissedEvent> dismissed = RecordEvents<DownloadDismissedEvent>();
        RecordingHandler<DownloadRetryScheduledEvent> retried = RecordEvents<DownloadRetryScheduledEvent>();
        string databaseName = await SeedAsync(
            new DownloadQueueItemBuilder()
                .WithEpisode(DownloadedEpisode())
                .Failed("ffmpeg download failed")
                .Build(),
            makeDue: true);
        using RuvarrDbContext dbContext = CreateDbContext(databaseName);

        // Act
        await CreateJob(dbContext).Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        dismissed.Handled.ShouldHaveSingleItem();
        retried.Handled.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenDueFailedItemIsNotSuperseded_RequeuesItem()
    {
        // Arrange
        RuvEpisode episode = new RuvEpisodeBuilder().BuildMatchedWithStatus(SonarrEpisodeStatus.Missing);
        string databaseName = await SeedAsync(
            new DownloadQueueItemBuilder().WithEpisode(episode).Failed("ffmpeg download failed").Build(),
            makeDue: true);
        using RuvarrDbContext dbContext = CreateDbContext(databaseName);

        // Act
        await CreateJob(dbContext).Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        using RuvarrDbContext verifyContext = CreateDbContext(databaseName);
        DownloadQueueItem updated = await verifyContext.Set<DownloadQueueItem>()
            .SingleAsync(TestContext.Current.CancellationToken);
        updated.Status.ShouldBe(DownloadQueueStatus.Pending);
    }

    [Fact]
    public async Task WhenPendingItemEpisodeIsDownloaded_LeavesItemUntouched()
    {
        // Arrange
        string databaseName = await SeedAsync(new DownloadQueueItemBuilder()
            .WithEpisode(DownloadedEpisode())
            .Build());
        using RuvarrDbContext dbContext = CreateDbContext(databaseName);

        // Act
        await CreateJob(dbContext).Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        using RuvarrDbContext verifyContext = CreateDbContext(databaseName);
        DownloadQueueItem updated = await verifyContext.Set<DownloadQueueItem>()
            .SingleAsync(TestContext.Current.CancellationToken);
        updated.Status.ShouldBe(DownloadQueueStatus.Pending);
    }

    [Fact]
    public async Task WhenDownloadingItemEpisodeIsDownloaded_LeavesItemUntouched()
    {
        // Arrange
        DownloadQueueItem item = new DownloadQueueItemBuilder().WithEpisode(DownloadedEpisode()).Build();
        item.MarkDownloading();
        string databaseName = await SeedAsync(item);
        using RuvarrDbContext dbContext = CreateDbContext(databaseName);

        // Act
        await CreateJob(dbContext).Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        using RuvarrDbContext verifyContext = CreateDbContext(databaseName);
        DownloadQueueItem updated = await verifyContext.Set<DownloadQueueItem>()
            .SingleAsync(TestContext.Current.CancellationToken);
        updated.Status.ShouldBe(DownloadQueueStatus.Downloading);
    }

    [Fact]
    public async Task WhenCompleteItemEpisodeIsDownloaded_LeavesItemUntouched()
    {
        // Arrange
        string databaseName = await SeedAsync(new DownloadQueueItemBuilder()
            .WithEpisode(DownloadedEpisode())
            .Downloaded()
            .Build());
        using RuvarrDbContext dbContext = CreateDbContext(databaseName);

        // Act
        await CreateJob(dbContext).Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        using RuvarrDbContext verifyContext = CreateDbContext(databaseName);
        DownloadQueueItem updated = await verifyContext.Set<DownloadQueueItem>()
            .SingleAsync(TestContext.Current.CancellationToken);
        updated.Status.ShouldBe(DownloadQueueStatus.Complete);
    }
}
