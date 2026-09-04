using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Jobs;
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

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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
        await sut.Execute(_context);

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
        await sut.Execute(_context);

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
        await sut.Execute(_context);

        // Assert
        DownloadQueueItem updated = await dbContext.Set<DownloadQueueItem>()
            .FirstAsync(TestContext.Current.CancellationToken);
        updated.Status.ShouldBe(DownloadQueueStatus.Exhausted);
    }
}
