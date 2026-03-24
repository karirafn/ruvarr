using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Settings;
using Ruvarr.TvdbEpisodeLookup.Jobs;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbEpisodeLookupJobTests;

public sealed class TitleMatching
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly TvdbEpisodeLookupNotifier _notifier = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public TitleMatching()
    {
        _sonarr.GetMissingEpisodesAsync().Returns([]);
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            TvdbApiKey: "tvdb-key", TmdbApiKey: "tmdb-key"));
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private TvdbEpisodeLookupJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<TvdbEpisodeLookupJob>.Instance,
        dbContext, _tvdb, _sonarr, _notifier, new DomainEventBroadcaster(), _settingsStore);

    [Fact]
    public async Task MatchesEpisodeWhenTranslationMatchesTitle()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder()
            .WithId(101)
            .WithSeasonNumber(1)
            .WithNumber(1)
            .WithNameTranslations("isl")
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 1", "", "isl", true));
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].TvdbId.ShouldBe(101);
    }

    [Fact]
    public async Task SkipsEpisodeWhenTranslationNotFound()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(101).WithNameTranslations("isl").Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder().WithId(102).WithNumber(2).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode, tvdbEpisode2).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((EpisodeTranslation?)null);
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipsEpisodeWhenNoIslTranslation()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(101).WithNameTranslations("eng").Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder().WithId(102).WithNumber(2).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode, tvdbEpisode2).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _ = _tvdb.DidNotReceive().GetEpisodeTranslationAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipsEpisodeWhenTitleDoesNotMatch()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(101).WithNameTranslations("isl").Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder().WithId(102).WithNumber(2).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode, tvdbEpisode2).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Different title", "", "isl", true));
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task FetchesAllTranslationsForMultipleEpisodes()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Þáttur 2", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0003", new Uri("http://test.com"), "Þáttur 3", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(101).WithSeasonNumber(1).WithNumber(1).WithNameTranslations("isl").Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(102).WithSeasonNumber(1).WithNumber(2).WithNameTranslations("isl").Build();
        Episode tvdbEpisode3 = new TvdbEpisodeDataBuilder()
            .WithId(103).WithSeasonNumber(1).WithNumber(3).WithNameTranslations("isl").Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisode2, tvdbEpisode3).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 1", "", "isl", true));
        _tvdb.GetEpisodeTranslationAsync(102, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 2", "", "isl", true));
        _tvdb.GetEpisodeTranslationAsync(103, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 3", "", "isl", true));
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].TvdbId.ShouldBe(101);
        program.Episodes[1].TvdbEpisodes[0].TvdbId.ShouldBe(102);
        program.Episodes[2].TvdbEpisodes[0].TvdbId.ShouldBe(103);
        await _tvdb.Received(3).GetEpisodeTranslationAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetsIsMissingWhenTvdbIdIsInSonarrList()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder()
            .WithId(101)
            .WithSeasonNumber(1)
            .WithNumber(1)
            .WithNameTranslations("isl")
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        MissingEpisode missingEpisode = new MissingEpisodeBuilder().WithTvdbId(101).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 1", "", "isl", true));
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([missingEpisode]);
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].IsMissing.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipsTranslationLookupForAlreadyMatchedEpisodes()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Þáttur 2", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        program.Episodes[0].Match(101, 1, 1, false);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(101).WithSeasonNumber(1).WithNumber(1).WithNameTranslations("isl").Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(102).WithSeasonNumber(1).WithNumber(2).WithNameTranslations("isl").Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisode2).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(102, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 2", "", "isl", true));
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        await _tvdb.DidNotReceive().GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _tvdb.Received(1).GetEpisodeTranslationAsync(102, Arg.Any<string>(), Arg.Any<CancellationToken>());
        program.Episodes[1].TvdbEpisodes[0].TvdbId.ShouldBe(102);
    }
}
