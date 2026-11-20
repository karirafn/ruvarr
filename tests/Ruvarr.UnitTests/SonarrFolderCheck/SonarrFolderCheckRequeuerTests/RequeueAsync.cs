using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;
using Ruvarr.SonarrFolderCheck;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.SonarrFolderCheck.SonarrFolderCheckRequeuerTests;

public sealed class RequeueAsync
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private const string Folder = "/downloads/tv";

    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public RequeueAsync()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options,
        _serviceProvider);

    private static SonarrFolderCheckResult Result(SonarrFolderCheckStatus status) => new(status, Folder, Now);

    private static RuvEpisode MissingEpisode() =>
        new RuvEpisodeBuilder().BuildMatchedWithStatus(SonarrEpisodeStatus.Missing);

    private static DownloadQueueItemBuilder FailedBecauseSonarrCannotSeeFolder() =>
        new DownloadQueueItemBuilder()
            .WithEpisode(MissingEpisode())
            .WithFailureKind(DownloadFailureKind.SonarrCannotSeeDownloadFolder)
            .Failed("Sonarr can't see the download folder");

    private async Task SeedAsync(DownloadQueueItem item)
    {
        using RuvarrDbContext seedContext = CreateDbContext();
        seedContext.Set<DownloadQueueItem>().Add(item);
        await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<DownloadQueueItem> RequeueAndReloadAsync(SonarrFolderCheckStatus status)
    {
        using (RuvarrDbContext dbContext = CreateDbContext())
        {
            await new SonarrFolderCheckRequeuer(dbContext, NullLogger<SonarrFolderCheckRequeuer>.Instance)
                .RequeueAsync(Result(status), TestContext.Current.CancellationToken);
        }

        using RuvarrDbContext verifyContext = CreateDbContext();
        return await verifyContext.Set<DownloadQueueItem>().SingleAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RequeuesFailedItem_WhenCheckPasses()
    {
        // Arrange
        await SeedAsync(FailedBecauseSonarrCannotSeeFolder().Build());

        // Act
        DownloadQueueItem item = await RequeueAndReloadAsync(SonarrFolderCheckStatus.Passing);

        // Assert
        item.ShouldSatisfyAllConditions(
            () => item.Status.ShouldBe(DownloadQueueStatus.Pending),
            () => item.FailureKind.ShouldBe(DownloadFailureKind.Unclassified),
            () => item.RetryCount.ShouldBe(1));
    }

    [Fact]
    public async Task RequeuesExhaustedItem_WhenCheckPasses()
    {
        // Arrange
        await SeedAsync(FailedBecauseSonarrCannotSeeFolder().Exhausted().Build());

        // Act
        DownloadQueueItem item = await RequeueAndReloadAsync(SonarrFolderCheckStatus.Passing);

        // Assert
        item.ShouldSatisfyAllConditions(
            () => item.Status.ShouldBe(DownloadQueueStatus.Pending),
            () => item.RetryCount.ShouldBe(RetrySchedule.MaxRetries + 1));
    }

    [Theory]
    [InlineData(SonarrFolderCheckStatus.Unknown)]
    [InlineData(SonarrFolderCheckStatus.Failing)]
    public async Task LeavesItemFailed_WhenCheckDoesNotPass(SonarrFolderCheckStatus status)
    {
        // Arrange
        await SeedAsync(FailedBecauseSonarrCannotSeeFolder().Build());

        // Act
        DownloadQueueItem item = await RequeueAndReloadAsync(status);

        // Assert
        item.ShouldSatisfyAllConditions(
            () => item.Status.ShouldBe(DownloadQueueStatus.Failed),
            () => item.FailureKind.ShouldBe(DownloadFailureKind.SonarrCannotSeeDownloadFolder));
    }

    [Fact]
    public async Task LeavesUnclassifiedFailure_WhenCheckPasses()
    {
        // Arrange
        await SeedAsync(new DownloadQueueItemBuilder().WithEpisode(MissingEpisode()).Failed("ffmpeg download failed").Build());

        // Act
        DownloadQueueItem item = await RequeueAndReloadAsync(SonarrFolderCheckStatus.Passing);

        // Assert
        item.Status.ShouldBe(DownloadQueueStatus.Failed);
    }

    [Fact]
    public async Task LeavesSupersededItemForDismissal_WhenCheckPasses()
    {
        // Arrange
        await SeedAsync(FailedBecauseSonarrCannotSeeFolder()
            .WithEpisode(new RuvEpisodeBuilder().BuildMatchedWithStatus(SonarrEpisodeStatus.Downloaded))
            .Build());

        // Act
        DownloadQueueItem item = await RequeueAndReloadAsync(SonarrFolderCheckStatus.Passing);

        // Assert
        item.ShouldSatisfyAllConditions(
            () => item.Status.ShouldBe(DownloadQueueStatus.Failed),
            () => item.FailureKind.ShouldBe(DownloadFailureKind.SonarrCannotSeeDownloadFolder));
    }
}
