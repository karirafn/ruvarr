using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Jobs;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.DownloadQueueProcessorTests;

public sealed class EarlyReturns
{
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly IFfmpegService _ffmpeg = Substitute.For<IFfmpegService>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public EarlyReturns()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private DownloadQueueProcessor CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<DownloadQueueProcessor>.Instance,
        dbContext, _sonarr, _ffmpeg, _settingsStore);

    private static async Task<DownloadQueueItem> SeedPendingItemAsync(RuvarrDbContext dbContext)
    {
        RuvProgram program = new RuvProgramBuilder().Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(program.Episodes[0]);
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item;
    }

    [Fact]
    public async Task MarksItemFailed_WhenDownloadsRootDirectoryIsEmpty()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedPendingItemAsync(dbContext);
        _settingsStore.Current.Returns(new RuvarrSettings(DownloadsRootDirectory: ""));
        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        item.Status.ShouldBe(DownloadQueueStatus.Failed);
    }

    [Fact]
    public async Task MarksItemFailed_WhenEpisodeDownloadDirectoryIsEmpty()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedPendingItemAsync(dbContext);
        _settingsStore.Current.Returns(new RuvarrSettings(
            DownloadsRootDirectory: "/downloads",
            EpisodeDownloadDirectory: ""));
        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        item.Status.ShouldBe(DownloadQueueStatus.Failed);
    }
}
