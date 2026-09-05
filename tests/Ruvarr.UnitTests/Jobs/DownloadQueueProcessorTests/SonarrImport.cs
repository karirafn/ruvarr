using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Domain;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.DownloadQueueProcessorTests;

public sealed class SonarrImport : IDisposable
{
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly RuvarrSettings _settings;
    private readonly string _tempDownloadsRoot;

    public SonarrImport()
    {
        _tempDownloadsRoot = Path.Combine(Path.GetTempPath(), $"ruvarr-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDownloadsRoot);

        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settings = new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            EpisodeDownloadDirectory: "episodes")
        {
            DownloadsRoot = _tempDownloadsRoot
        };
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

    /// <summary>
    /// Replays the production transitions the job performs before delegating to the importer.
    /// Returns the fileName and completedPath the job would have computed.
    /// </summary>
    private async Task<(string FileName, string CompletedPath)> ArrangePostDownloadStateAsync(
        DownloadQueueItem item,
        RuvarrDbContext dbContext)
    {
        item.MarkDownloading();
        item.MarkDownloaded();
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        string fileName = item.FileName!;
        string completedPath = DownloadFileStore.CompletedPath(_settings, fileName);
        return (fileName, completedPath);
    }

    private static async Task WriteCompletedFileAsync(string completedPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(completedPath)!);
        await File.WriteAllTextAsync(completedPath, "fake", CancellationToken.None);
    }

    [Fact]
    public async Task ImportsEpisode_WhenSeriesExistsInSonarr()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedMatchedEpisodeAsync(dbContext);
        (string fileName, string completedPath) = await ArrangePostDownloadStateAsync(item, dbContext);
        await WriteCompletedFileAsync(completedPath);

        // file.Episodes has 4 entries (divergent from Ruvarr's 1 TVDB link) to prove
        // the import uses TvdbId resolution, not file.Episodes
        ManualImportFile file = CreateManualImportFile(
            seriesId: 42,
            episodeIds: [101, 102, 103, 104]);

        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Series>());
        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([file]);
        _sonarr.GetEpisodesAsync(42, Arg.Any<CancellationToken>())
            .Returns(new[] { new SonarrEpisode(Id: 201, SeriesId: 42, TvdbId: 5001, SeasonNumber: 1, EpisodeNumber: 1) });

        SonarrImporter sut = new(_sonarr, dbContext, NullLogger<SonarrImporter>.Instance);

        // Act
        await sut.ImportAsync(item, _settings, fileName, completedPath, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.Received(1).GetManualImportsAsync(
            Arg.Any<string>(),
            null,
            Arg.Any<CancellationToken>());

        // Episode ids come from TVDB-id join (201), not from file.Episodes ([101,102,103,104])
        int[] expectedEpisodeIds = [201];
        await _sonarr.Received(1).ManualImportFilesAsync(
            Arg.Is<IEnumerable<ManualImportRequest>>(reqs =>
                reqs.First().SeriesId == 42 &&
                reqs.First().EpisodeIds.SequenceEqual(expectedEpisodeIds) &&
                reqs.First().Path == completedPath),
            Arg.Any<CancellationToken>());

        item.Status.ShouldBe(DownloadQueueStatus.Complete);
    }

    [Fact]
    public async Task SkipsImport_WhenSeriesIsNullAndSonarrSeriesIdIsNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedMatchedEpisodeAsync(dbContext);
        (string fileName, string completedPath) = await ArrangePostDownloadStateAsync(item, dbContext);

        ManualImportFile file = CreateManualImportFile(
            seriesId: null,
            episodeIds: [101]);

        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Series>());
        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([file]);

        SonarrImporter sut = new(_sonarr, dbContext, NullLogger<SonarrImporter>.Instance);

        // Act
        await sut.ImportAsync(item, _settings, fileName, completedPath, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.DidNotReceive().GetEpisodesAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
        await _sonarr.DidNotReceive().ManualImportFilesAsync(
            Arg.Any<IEnumerable<ManualImportRequest>>(),
            Arg.Any<CancellationToken>());

        item.Status.ShouldBe(DownloadQueueStatus.Failed);
        item.FailureReason.ShouldBe("Sonarr has no matching series");
    }

    [Fact]
    public async Task WhenTvdbLinksHaveNoSonarrMatch_MarksItemFailed_AndSkipsImport()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedMatchedEpisodeAsync(dbContext);
        (string fileName, string completedPath) = await ArrangePostDownloadStateAsync(item, dbContext);
        await WriteCompletedFileAsync(completedPath);

        ManualImportFile file = CreateManualImportFile(
            seriesId: 42,
            episodeIds: []);

        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Series>());
        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([file]);
        // Sonarr episode has a different TvdbId — no match for episode's TvdbId 5001
        _sonarr.GetEpisodesAsync(42, Arg.Any<CancellationToken>())
            .Returns(new[] { new SonarrEpisode(Id: 201, SeriesId: 42, TvdbId: 9999, SeasonNumber: 1, EpisodeNumber: 1) });

        SonarrImporter sut = new(_sonarr, dbContext, NullLogger<SonarrImporter>.Instance);

        // Act
        await sut.ImportAsync(item, _settings, fileName, completedPath, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.DidNotReceive().ManualImportFilesAsync(
            Arg.Any<IEnumerable<ManualImportRequest>>(),
            Arg.Any<CancellationToken>());

        item.Status.ShouldBe(DownloadQueueStatus.Failed);
        item.FailureReason.ShouldBe("Sonarr is missing episodes");
    }

    [Fact]
    public async Task ImportsEpisode_WhenSeriesIsNullButSonarrSeriesIdKnown()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedMatchedEpisodeAsync(dbContext);
        (string fileName, string completedPath) = await ArrangePostDownloadStateAsync(item, dbContext);
        await WriteCompletedFileAsync(completedPath);

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

        SonarrImporter sut = new(_sonarr, dbContext, NullLogger<SonarrImporter>.Instance);

        // Act
        await sut.ImportAsync(item, _settings, fileName, completedPath, TestContext.Current.CancellationToken);

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
        DownloadQueueItem item = await SeedMatchedEpisodeAsync(dbContext);
        (string fileName, string completedPath) = await ArrangePostDownloadStateAsync(item, dbContext);
        await WriteCompletedFileAsync(completedPath);

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

        SonarrImporter sut = new(_sonarr, dbContext, NullLogger<SonarrImporter>.Instance);

        // Act
        await sut.ImportAsync(item, _settings, fileName, completedPath, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.DidNotReceive().ManualImportFilesAsync(
            Arg.Any<IEnumerable<ManualImportRequest>>(),
            Arg.Any<CancellationToken>());

        item.Status.ShouldBe(DownloadQueueStatus.Failed);
        item.FailureReason.ShouldBe("Sonarr is missing episodes");
    }

    [Fact]
    public async Task ImportsEpisode_WithMultipleEpisodes_WhenSeriesIsNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedMatchedEpisodeWithMultipleTvdbEpisodesAsync(dbContext);
        (string fileName, string completedPath) = await ArrangePostDownloadStateAsync(item, dbContext);
        await WriteCompletedFileAsync(completedPath);

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

        SonarrImporter sut = new(_sonarr, dbContext, NullLogger<SonarrImporter>.Instance);

        // Act
        await sut.ImportAsync(item, _settings, fileName, completedPath, TestContext.Current.CancellationToken);

        // Assert
        int[] expectedEpisodeIds = [201, 202];
        await _sonarr.Received(1).ManualImportFilesAsync(
            Arg.Is<IEnumerable<ManualImportRequest>>(reqs =>
                reqs.First().SeriesId == 42 &&
                reqs.First().EpisodeIds.SequenceEqual(expectedEpisodeIds)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenSonarrScanDoesNotIncludeFile_MarksItemFailed_WithoutThrowing()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedMatchedEpisodeAsync(dbContext);
        (string fileName, string completedPath) = await ArrangePostDownloadStateAsync(item, dbContext);

        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Series>());
        // Return a file with a different path — the downloaded filename is not in the scan results
        ManualImportFile unrelatedFile = CreateManualImportFile(
            seriesId: 42,
            episodeIds: [],
            filename: "Some.Other.Show.S01E01-RUV.mp4");
        _sonarr.GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([unrelatedFile]);

        SonarrImporter sut = new(_sonarr, dbContext, NullLogger<SonarrImporter>.Instance);

        // Act
        await sut.ImportAsync(item, _settings, fileName, completedPath, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.DidNotReceive().ManualImportFilesAsync(
            Arg.Any<IEnumerable<ManualImportRequest>>(),
            Arg.Any<CancellationToken>());

        item.Status.ShouldBe(DownloadQueueStatus.Failed);
        item.FailureReason.ShouldBe("Sonarr scan did not include the file");
    }

    private static async Task<DownloadQueueItem> SeedUnmatchedEpisodeAsync(RuvarrDbContext dbContext)
    {
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com/stream"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        RuvEpisode episode = program.Episodes[0];
        // Deliberately no episode.Match(...) — TvdbEpisodes remains empty
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(episode);
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item;
    }

    [Fact]
    public async Task WhenEpisodeHasNoTvdbMatch_SkipsImport_AndDoesNotMarkFailed()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedUnmatchedEpisodeAsync(dbContext);
        (string fileName, string completedPath) = await ArrangePostDownloadStateAsync(item, dbContext);

        SonarrImporter sut = new(_sonarr, dbContext, NullLogger<SonarrImporter>.Instance);

        // Act
        await sut.ImportAsync(item, _settings, fileName, completedPath, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.DidNotReceive().GetSeriesAsync(Arg.Any<CancellationToken>());
        await _sonarr.DidNotReceive().ManualImportFilesAsync(
            Arg.Any<IEnumerable<ManualImportRequest>>(),
            Arg.Any<CancellationToken>());

        item.Status.ShouldBe(DownloadQueueStatus.Complete);
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
            Path: $"{_tempDownloadsRoot}/episodes/{filename}",
            RelativePath: filename,
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
