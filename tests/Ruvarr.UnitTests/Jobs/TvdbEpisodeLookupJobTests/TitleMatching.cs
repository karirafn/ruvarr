using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
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

    public TitleMatching()
    {
        _sonarr.GetMissingEpisodesAsync().Returns([]);
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private TvdbEpisodeLookupJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<TvdbEpisodeLookupJob>.Instance,
        dbContext, _tvdb, _sonarr, _notifier);

    [Fact]
    public async Task MatchesEpisodeWhenTranslationMatchesTitle()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow);
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
        program.Episodes[0].TvdbId.ShouldBe(101);
    }

    [Fact]
    public async Task SkipsEpisodeWhenTranslationNotFound()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow);
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
        program.Episodes[0].TvdbId.ShouldBeNull();
    }

    [Fact]
    public async Task SkipsEpisodeWhenNoIslTranslation()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow);
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
        program.Episodes[0].TvdbId.ShouldBeNull();
    }

    [Fact]
    public async Task SkipsEpisodeWhenTitleDoesNotMatch()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow);
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
        program.Episodes[0].TvdbId.ShouldBeNull();
    }

    [Fact]
    public async Task SetsIsMissingWhenTvdbIdIsInSonarrList()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow);
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
        program.Episodes[0].IsMissing.ShouldBeTrue();
    }
}
