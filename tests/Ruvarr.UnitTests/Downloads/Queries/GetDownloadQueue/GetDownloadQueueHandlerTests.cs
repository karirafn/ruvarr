using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Queries.GetDownloadQueue;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Queries.GetDownloadQueue;

public sealed class GetDownloadQueueHandlerTests
{
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public GetDownloadQueueHandlerTests()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    [Fact]
    public async Task WhenFailedItemExists_ReturnsItemWithFailureFields()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(42)
            .WithName("Test Program")
            .Build();
        program.TryAddEpisode("ep001", new Uri("http://ruv.is/ep1"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = new DownloadQueueItemBuilder()
            .WithEpisode(program.Episodes[0])
            .Failed("FFmpeg download failed")
            .Build();
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        GetDownloadQueueHandler sut = new(dbContext);

        // Act
        IReadOnlyList<DownloadQueueItemSummary> result = await sut.Handle(
            new GetDownloadQueueQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        DownloadQueueItemSummary summary = result[0];
        summary.ShouldSatisfyAllConditions(
            () => summary.FailureReason.ShouldBe("FFmpeg download failed"),
            () => summary.RetryCount.ShouldBe(1),
            () => summary.NextRetryAt.ShouldNotBeNull(),
            () => summary.Status.ShouldBe(DownloadQueueStatus.Failed),
            () => summary.ProgramName.ShouldBe("Test Program"),
            () => summary.ProgramRuvId.ShouldBe(42),
            () => summary.EpisodeTitle.ShouldBe("Episode 1"),
            () => summary.EpisodeRuvId.ShouldBe("ep001"));
    }

    [Fact]
    public async Task WhenMultipleItemsExist_OrdersActionableItemsFirst()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        RuvProgram program = new RuvProgramBuilder().Build();
        program.TryAddEpisode("ep-pending", new Uri("http://ruv.is/ep1"), "Pending Episode", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep-downloading", new Uri("http://ruv.is/ep2"), "Downloading Episode", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep-failed", new Uri("http://ruv.is/ep3"), "Failed Episode", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep-exhausted", new Uri("http://ruv.is/ep4"), "Exhausted Episode", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem pendingItem = new DownloadQueueItemBuilder()
            .WithEpisode(program.Episodes.First(e => e.RuvId == "ep-pending"))
            .Build();

        DownloadQueueItem downloadingItem = DownloadQueueItem.Create(program.Episodes.First(e => e.RuvId == "ep-downloading"));
        downloadingItem.MarkDownloading();

        DownloadQueueItem failedItem = new DownloadQueueItemBuilder()
            .WithEpisode(program.Episodes.First(e => e.RuvId == "ep-failed"))
            .Failed("Sonarr import failed")
            .Build();

        DownloadQueueItem exhaustedItem = new DownloadQueueItemBuilder()
            .WithEpisode(program.Episodes.First(e => e.RuvId == "ep-exhausted"))
            .Exhausted()
            .Build();

        dbContext.Set<DownloadQueueItem>().AddRange(pendingItem, downloadingItem, failedItem, exhaustedItem);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        GetDownloadQueueHandler sut = new(dbContext);

        // Act
        IReadOnlyList<DownloadQueueItemSummary> result = await sut.Handle(
            new GetDownloadQueueQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(4);

        // Failed and Exhausted come first
        IReadOnlyList<DownloadQueueStatus> firstTwoStatuses = [result[0].Status, result[1].Status];
        firstTwoStatuses.ShouldContain(DownloadQueueStatus.Failed);
        firstTwoStatuses.ShouldContain(DownloadQueueStatus.Exhausted);

        // Then Downloading
        result[2].Status.ShouldBe(DownloadQueueStatus.Downloading);

        // Then Pending last
        result[3].Status.ShouldBe(DownloadQueueStatus.Pending);
    }

    [Fact]
    public async Task WhenExhaustedItemExists_ReturnsWithNullNextRetryAt()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        RuvProgram program = new RuvProgramBuilder().WithRuvId(99).WithName("Exhausted Show").Build();
        program.TryAddEpisode("ep-ex", new Uri("http://ruv.is/ex"), "Exhausted Episode", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = new DownloadQueueItemBuilder()
            .WithEpisode(program.Episodes[0])
            .Exhausted()
            .Build();
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        GetDownloadQueueHandler sut = new(dbContext);

        // Act
        IReadOnlyList<DownloadQueueItemSummary> result = await sut.Handle(
            new GetDownloadQueueQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        DownloadQueueItemSummary summary = result[0];
        summary.ShouldSatisfyAllConditions(
            () => summary.Status.ShouldBe(DownloadQueueStatus.Exhausted),
            () => summary.NextRetryAt.ShouldBeNull(),
            () => summary.RetryCount.ShouldBeGreaterThan(0),
            () => summary.ProgramName.ShouldBe("Exhausted Show"),
            () => summary.ProgramRuvId.ShouldBe(99));
    }

    [Fact]
    public async Task WhenNoItems_ReturnsEmptyList()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        GetDownloadQueueHandler sut = new(dbContext);

        // Act
        IReadOnlyList<DownloadQueueItemSummary> result = await sut.Handle(
            new GetDownloadQueueQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }
}
