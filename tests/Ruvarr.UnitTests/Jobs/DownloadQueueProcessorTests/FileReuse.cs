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
using Ruvarr.Infrastructure.Sonarr.Models;
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

        item.Status.ShouldBe(DownloadQueueStatus.Complete);
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
        item.Status.ShouldBe(DownloadQueueStatus.Complete);
    }

    // Primary scenario: failed during Sonarr import, completed file already on disk.
    // Retry reuses the file and re-attempts the import — ffmpeg must NOT run.
    [Fact]
    public async Task WhenCompletedFileExists_AndEpisodeIsTvdbMatched_ReusesFile_AndInvokesManualImport()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedMatchedEpisodeAsync(dbContext);

        // Pre-create the completed file so the reuse branch is taken.
        string fileName = item.Episode.ToFilename();
        string completedDir = _settings.ResolvedEpisodeDownloadDirectory;
        Directory.CreateDirectory(completedDir);
        string completedPath = DownloadFileStore.CompletedPath(_settings, fileName);
        await File.WriteAllTextAsync(completedPath, "existing-episode-data", TestContext.Current.CancellationToken);

        // Sonarr scan returns the file at the completed path (file already matches by name).
        ManualImportFile scanFile = CreateManualImportFile(seriesId: 42, episodeIds: [], fileName: fileName);
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Series>());
        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([scanFile]);
        _sonarr.GetEpisodesAsync(42, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new SonarrEpisode(Id: 201, SeriesId: 42, TvdbId: 5001, SeasonNumber: 1, EpisodeNumber: 1),
            });

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert: ffmpeg was NOT called — file was reused from disk
        await _ffmpeg.DidNotReceive().DownloadAsync(
            Arg.Any<Uri>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IProgress<FfmpegProgressData>>(),
            Arg.Any<CancellationToken>());

        // Assert: Sonarr manual import was called with the completed file path
        await _sonarr.Received(1).ManualImportFilesAsync(
            Arg.Is<IEnumerable<ManualImportRequest>>(reqs =>
                reqs.First().Path == completedPath),
            Arg.Any<CancellationToken>());

        item.Status.ShouldBe(DownloadQueueStatus.Complete);
    }

    private static async Task<DownloadQueueItem> SeedMatchedEpisodeAsync(RuvarrDbContext dbContext)
    {
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

        return item;
    }

    private ManualImportFile CreateManualImportFile(int seriesId, IReadOnlyList<int> episodeIds, string fileName)
    {
        Series series = new(
            Title: "Test Series",
            SortTitle: "test series",
            Status: "continuing",
            Ended: false,
            Overview: "",
            Airtime: new TimeOnly(20, 0),
            Originallanguage: new Originallanguage(1, "English"),
            Year: 2024,
            Path: "/tv/test",
            QualityProfileId: 1,
            SeasonFolder: true,
            Monitored: true,
            MonitorNewItems: "all",
            UseScheneNumbering: false,
            Runtime: 30,
            TvdbId: 5000,
            TvRageId: 0,
            TvMazeId: 0,
            TmdbId: 0,
            SeriesType: "standard",
            CleanTitle: "testseries",
            ImdbId: "",
            TitleSlug: "test-series",
            Certification: "",
            FirstAired: DateTime.UtcNow,
            LastAired: DateTime.UtcNow,
            Added: DateTime.UtcNow,
            Images: [],
            Seasons: [],
            Genres: [],
            Ratings: new Ratings(0, 0),
            LanguageProfileId: 1,
            Id: seriesId);

        IReadOnlyList<ManualImportEpisode> episodes = episodeIds
            .Select(id => new ManualImportEpisode(
                SeriesId: seriesId,
                TvdbId: 5001,
                EpisodeFileId: 0,
                SeasonNumber: 1,
                EpisodeNumber: 1,
                Title: "Episode 1",
                Airdate: DateOnly.FromDateTime(DateTime.UtcNow),
                AirDateUtc: DateTime.UtcNow,
                LastSearchTime: DateTime.UtcNow,
                Runtime: 30,
                HasFile: false,
                Monitored: true,
                UnverifiedSceneNumbering: false,
                Id: id))
            .ToList();

        return new ManualImportFile(
            Path: Path.Join(_settings.ResolvedEpisodeDownloadDirectory, fileName),
            RelativePath: fileName,
            Name: fileName,
            Size: 1000,
            Series: series,
            SeasonNumber: 1,
            Episodes: episodes,
            Quality: new QualityContainer(new Quality(1, "HDTV-720p", "HDTV-720p", 720), new Revision(1, 1, false)),
            Languages: [new Language(1, "English")],
            QualityWeight: 1,
            CustomFormatScore: 0,
            IndexerFlags: 0,
            ReleaseType: "unknown",
            Id: 1);
    }
}
