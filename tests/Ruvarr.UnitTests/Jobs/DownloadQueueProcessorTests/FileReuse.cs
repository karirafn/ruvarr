using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

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

/// <summary>
/// Tests for the file-reuse branch: when a retry finds the completed file already on disk,
/// the processor skips ffmpeg download + trim + move and proceeds straight to Sonarr import.
/// Covers the three edge cases from the acceptance criteria.
/// </summary>
public sealed class FileReuse : IDisposable
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
    private readonly RuvarrSettings _settings;

    public FileReuse()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ruvarr-fr-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRoot);

        _settings = new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr",
            SonarrApiKey: "key",
            EpisodeDownloadDirectory: "episodes")
        {
            DownloadsRoot = _tempRoot
        };

        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(_settings);

        _fileStore = new DownloadFileStore(NullLogger<DownloadFileStore>.Instance);
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

    private static async Task<DownloadQueueItem> SeedPendingItemAsync(RuvarrDbContext dbContext)
    {
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com/stream"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(program.Episodes[0]);
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item;
    }

    // Edge case 2: failed AFTER successful download+move but during import.
    // Completed file is present → reuse: skip ffmpeg, proceed to Sonarr import path.
    [Fact]
    public async Task WhenCompletedFileAlreadyExists_SkipsFfmpegDownload_AndProceedsToSonarrImport()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedPendingItemAsync(dbContext);

        // Pre-compute the file name the episode will get when MarkDownloading() is called.
        // MarkDownloading sets FileName = Episode.ToFilename(); replicate that here.
        string expectedFileName = item.Episode.ToFilename();
        string completedDir = _settings.ResolvedEpisodeDownloadDirectory;
        Directory.CreateDirectory(completedDir);
        string completedPath = DownloadFileStore.CompletedPath(_settings, expectedFileName);
        await File.WriteAllTextAsync(completedPath, "existing-episode-data", TestContext.Current.CancellationToken);

        // Sonarr returns no series or scan results — Sonarr import will early-exit gracefully
        // (no TVDB episodes matched, so import is skipped by the existing early-return guard)
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Ruvarr.Infrastructure.Sonarr.Models.Series>>([]));
        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Ruvarr.Infrastructure.Sonarr.Models.ManualImportFile>>([]));


        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert: ffmpeg download was NOT called
        await _ffmpeg.DidNotReceive().DownloadAsync(
            Arg.Any<Uri>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IProgress<FfmpegProgressData>>(),
            Arg.Any<CancellationToken>());

        // Item has moved past Downloading (reached MarkDownloaded at minimum)
        item.Status.ShouldNotBe(DownloadQueueStatus.Downloading);
        item.Status.ShouldNotBe(DownloadQueueStatus.Pending);
    }

    // Edge case 1: failed BEFORE download finished → completed file absent → fresh download.
    [Fact]
    public async Task WhenCompletedFileAbsent_PerformsFreshDownload()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedPendingItemAsync(dbContext);

        // No completed file exists — completed directory is empty / doesn't exist.
        bool ffmpegCalled = false;
        _ffmpeg
            .DownloadAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<FfmpegProgressData>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                ffmpegCalled = true;
                string targetPath = callInfo.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await File.WriteAllTextAsync(targetPath, "downloaded-content", CancellationToken.None);
            });

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert: a fresh download was attempted
        ffmpegCalled.ShouldBeTrue();
    }

    // Edge case 3: failed and file manually deleted → completed file absent → fresh download.
    [Fact]
    public async Task WhenCompletedFileWasManuallyDeleted_PerformsFreshDownload()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedPendingItemAsync(dbContext);

        // Create the completed directory but do NOT write the file — simulates manual deletion.
        string completedDir = _settings.ResolvedEpisodeDownloadDirectory;
        Directory.CreateDirectory(completedDir);

        bool ffmpegCalled = false;
        _ffmpeg
            .DownloadAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<FfmpegProgressData>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                ffmpegCalled = true;
                string targetPath = callInfo.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await File.WriteAllTextAsync(targetPath, "downloaded-content", CancellationToken.None);
            });

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert: a fresh download was performed (file was absent even though dir existed)
        ffmpegCalled.ShouldBeTrue();
        item.Status.ShouldNotBe(DownloadQueueStatus.Downloading);
    }
}
