using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Notifiers;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Jobs;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.DownloadQueueProcessorTests;

public sealed class ExceptionHandling : IDisposable
{
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly IFfmpegService _ffmpeg = Substitute.For<IFfmpegService>();
    private readonly IRuvStreamInspector _streamInspector = Substitute.For<IRuvStreamInspector>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly DownloadProgressNotifier _progressNotifier = new(
        Substitute.For<IDomainEventBroadcaster>(), TimeProvider.System);
    private readonly DownloadFileStore _fileStore;
    private readonly string _tempRoot;

    public ExceptionHandling()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ruvarr-eh-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRoot);

        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            EpisodeDownloadDirectory: "episodes")
        {
            DownloadsRoot = _tempRoot
        });

        _fileStore = new DownloadFileStore(NullLogger<DownloadFileStore>.Instance);

        _ffmpeg
            .DownloadAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<FfmpegProgressData>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                string targetPath = callInfo.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await File.WriteAllTextAsync(targetPath, "fake", CancellationToken.None);
            });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private DownloadQueueProcessor CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<DownloadQueueProcessor>.Instance,
        dbContext, _sonarr, _ffmpeg, _streamInspector, _settingsStore, _progressNotifier, _fileStore);

    [Fact]
    public async Task WhenFfmpegThrowsOperationCanceledException_PropagatesAndDoesNotMarkFailed()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        RuvProgram program = new RuvProgramBuilder().WithRuvId(2).Build();
        program.TryAddEpisode("ep0002", new Uri("http://test.com/stream"), "Episode 2", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(program.Episodes[0]);
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _ffmpeg
            .DownloadAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<FfmpegProgressData>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("Scheduler shutting down"));

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act / Assert — OCE must propagate out of Execute(); it must not be swallowed into MarkFailed
        await Should.ThrowAsync<OperationCanceledException>(() => sut.Execute(_context));

        // Assert — item stays Downloading (not Failed); IncompleteDownloadCleanupService reclaims it at startup
        item.Status.ShouldNotBe(DownloadQueueStatus.Failed);
    }

    [Fact]
    public async Task MarksItemFailed_WhenSonarrThrowsDuringPostDownload()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        TvdbSeries series = new TvdbSeriesBuilder().WithId(5000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com/stream"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        RuvEpisode episode = program.Episodes[0];
        episode.Match(tvdbId: 5001, season: 1, episode: 1, isMissing: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(episode);
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Sonarr unavailable"));

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        item.Status.ShouldBe(DownloadQueueStatus.Failed);
        item.FailureReason.ShouldBe("Sonarr import failed");
    }
}
