using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Downloads.Domain;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Jobs;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

namespace Ruvarr.UnitTests.Jobs.DownloadQueueProcessorTests;

public sealed class SonarrImport
{
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly IFfmpegService _ffmpeg = Substitute.For<IFfmpegService>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public SonarrImport()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            EpisodeDownloadDirectory: "episodes"));
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private DownloadQueueProcessor CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<DownloadQueueProcessor>.Instance,
        dbContext, _sonarr, _ffmpeg, _settingsStore);

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

        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([file]);

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        int[] expectedEpisodeIds = [101];
        await _sonarr.Received(1).ManualImportFilesAsync(
            Arg.Is<IEnumerable<ManualImportRequest>>(reqs =>
                reqs.First().SeriesId == 42 &&
                reqs.First().EpisodeIds.SequenceEqual(expectedEpisodeIds)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipsImport_WhenSeriesIsNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedMatchedEpisodeAsync(dbContext);

        ManualImportFile file = CreateManualImportFile(
            seriesId: null,
            episodeIds: [101]);

        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([file]);

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
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

        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([file]);

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        await _sonarr.DidNotReceive().ManualImportFilesAsync(
            Arg.Any<IEnumerable<ManualImportRequest>>(),
            Arg.Any<CancellationToken>());
    }

    private static ManualImportFile CreateManualImportFile(
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
            Path: $"/downloads/episodes/test/{filename}",
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
