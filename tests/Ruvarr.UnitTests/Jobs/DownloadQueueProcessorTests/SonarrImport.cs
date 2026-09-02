using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
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

namespace Ruvarr.UnitTests.Jobs.DownloadQueueProcessorTests;

public sealed class SonarrImport : IDisposable
{
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly IFfmpegService _ffmpeg = Substitute.For<IFfmpegService>();
    private readonly IRuvStreamInspector _streamInspector = Substitute.For<IRuvStreamInspector>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly DownloadProgressNotifier _progressNotifier = new(
        Substitute.For<IDomainEventBroadcaster>(), TimeProvider.System);
    private readonly string _tempDownloadsRoot;

    public SonarrImport()
    {
        _tempDownloadsRoot = Path.Combine(Path.GetTempPath(), $"ruvarr-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDownloadsRoot);

        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            EpisodeDownloadDirectory: "episodes")
        {
            DownloadsRoot = _tempDownloadsRoot
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDownloadsRoot))
        {
            Directory.Delete(_tempDownloadsRoot, recursive: true);
        }
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private DownloadQueueProcessor CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<DownloadQueueProcessor>.Instance,
        dbContext, _sonarr, _ffmpeg, _streamInspector, _settingsStore, _progressNotifier);

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

    [Fact]
    public async Task ImportsEpisode_WhenSeriesExistsInSonarr()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedMatchedEpisodeAsync(dbContext);

        ManualImportFile file = CreateManualImportFile(
            seriesId: 42,
            episodeIds: [101]);

        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([file]);

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        await _sonarr.Received(1).GetManualImportsAsync(
            Arg.Any<string>(),
            null,
            Arg.Any<CancellationToken>());

        int[] expectedEpisodeIds = [101];
        await _sonarr.Received(1).ManualImportFilesAsync(
            Arg.Is<IEnumerable<ManualImportRequest>>(reqs =>
                reqs.First().SeriesId == 42 &&
                reqs.First().EpisodeIds.SequenceEqual(expectedEpisodeIds)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipsImport_WhenSeriesIsNullAndSonarrSeriesIdIsNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedMatchedEpisodeAsync(dbContext);

        ManualImportFile file = CreateManualImportFile(
            seriesId: null,
            episodeIds: [101]);

        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Series>());
        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([file]);

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        await _sonarr.DidNotReceive().GetEpisodesAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
        await _sonarr.DidNotReceive().ManualImportFilesAsync(
            Arg.Any<IEnumerable<ManualImportRequest>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipsImport_WhenEpisodesListIsEmpty()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedMatchedEpisodeAsync(dbContext);

        ManualImportFile file = CreateManualImportFile(
            seriesId: 42,
            episodeIds: []);

        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([file]);

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        await _sonarr.DidNotReceive().ManualImportFilesAsync(
            Arg.Any<IEnumerable<ManualImportRequest>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportsEpisode_WhenSeriesIsNullButSonarrSeriesIdKnown()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedMatchedEpisodeAsync(dbContext);

        ManualImportFile file = CreateManualImportFile(
            seriesId: null,
            episodeIds: []);

        Series sonarrSeries = CreateSonarrSeries(id: 42, tvdbId: 5000);
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { sonarrSeries });
        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([file]);
        _sonarr.GetEpisodesAsync(42, Arg.Any<CancellationToken>())
            .Returns(new[] { new SonarrEpisode(Id: 201, SeriesId: 42, TvdbId: 5001, SeasonNumber: 1, EpisodeNumber: 1) });

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        int[] expectedEpisodeIds = [201];
        await _sonarr.Received(1).ManualImportFilesAsync(
            Arg.Is<IEnumerable<ManualImportRequest>>(reqs =>
                reqs.First().SeriesId == 42 &&
                reqs.First().EpisodeIds.SequenceEqual(expectedEpisodeIds)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipsImport_WhenSeriesIsNullAndNoEpisodesMatch()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedMatchedEpisodeAsync(dbContext);

        ManualImportFile file = CreateManualImportFile(
            seriesId: null,
            episodeIds: []);

        Series sonarrSeries = CreateSonarrSeries(id: 42, tvdbId: 5000);
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { sonarrSeries });
        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([file]);
        _sonarr.GetEpisodesAsync(42, Arg.Any<CancellationToken>())
            .Returns(new[] { new SonarrEpisode(Id: 201, SeriesId: 42, TvdbId: 9999, SeasonNumber: 99, EpisodeNumber: 99) });

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        await _sonarr.DidNotReceive().ManualImportFilesAsync(
            Arg.Any<IEnumerable<ManualImportRequest>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportsEpisode_WithMultipleEpisodes_WhenSeriesIsNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedMatchedEpisodeWithMultipleTvdbEpisodesAsync(dbContext);

        ManualImportFile file = CreateManualImportFile(
            seriesId: null,
            episodeIds: [],
            filename: "Test.series.S01E01E02-RUV.mp4");

        Series sonarrSeries = CreateSonarrSeries(id: 42, tvdbId: 5000);
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { sonarrSeries });
        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([file]);
        _sonarr.GetEpisodesAsync(42, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new SonarrEpisode(Id: 201, SeriesId: 42, TvdbId: 5001, SeasonNumber: 1, EpisodeNumber: 1),
                new SonarrEpisode(Id: 202, SeriesId: 42, TvdbId: 5002, SeasonNumber: 1, EpisodeNumber: 2),
                new SonarrEpisode(Id: 203, SeriesId: 42, TvdbId: 5003, SeasonNumber: 2, EpisodeNumber: 1),
            });

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        int[] expectedEpisodeIds = [201, 202];
        await _sonarr.Received(1).ManualImportFilesAsync(
            Arg.Is<IEnumerable<ManualImportRequest>>(reqs =>
                reqs.First().SeriesId == 42 &&
                reqs.First().EpisodeIds.SequenceEqual(expectedEpisodeIds)),
            Arg.Any<CancellationToken>());
    }

    private static async Task<DownloadQueueItem> SeedMatchedEpisodeWithMultipleTvdbEpisodesAsync(RuvarrDbContext dbContext)
    {
        TvdbSeries series = new TvdbSeriesBuilder().WithId(5000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com/stream"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        RuvEpisode episode = program.Episodes[0];
        episode.MatchMultiple([
            TvdbEpisode.Create(tvdbId: 5001, seasonNumber: 1, episodeNumber: 1, isMissing: true),
            TvdbEpisode.Create(tvdbId: 5002, seasonNumber: 1, episodeNumber: 2, isMissing: true),
        ]);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(episode);
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item;
    }

    private static Series CreateSonarrSeries(int id, int tvdbId) => new(
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
        TvdbId: tvdbId,
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
        Id: id);

    private ManualImportFile CreateManualImportFile(
        int? seriesId,
        IReadOnlyList<int> episodeIds,
        string filename = "Test.series.S01E01-RUV.mp4")
    {
        Series? series = seriesId is not null
            ? new Series(
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
                Id: seriesId.Value)
            : null;

        IReadOnlyList<ManualImportEpisode> episodes = episodeIds.Select(id =>
            new ManualImportEpisode(
                SeriesId: seriesId ?? 0,
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
                Id: id)).ToList();

        return new ManualImportFile(
            Path: $"{_tempDownloadsRoot}/episodes/test/{filename}",
            RelativePath: $"test/{filename}",
            Name: filename,
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
