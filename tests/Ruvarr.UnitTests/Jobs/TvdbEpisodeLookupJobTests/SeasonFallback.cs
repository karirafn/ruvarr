using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Jobs;
using Ruvarr.Programs;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbEpisodeLookupJobTests;

public sealed class SeasonFallback
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly TvdbEpisodeLookupNotifier _notifier = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public SeasonFallback()
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
    public async Task MatchesEpisodeViaFallbackWhenTitleMatchFails()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBe(201);
        program.Episodes[0].SeasonNumber.ShouldBe(2);
    }

    [Fact]
    public async Task SkipsFallbackWhenSeasonIsZero()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBeNull();
        program.Episodes[0].NextLookup.ShouldNotBeNull();
    }

    [Fact]
    public async Task SkipsFallbackWhenEpisodeCountMismatch()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder().WithId(202).WithSeasonNumber(2).WithNumber(2).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000)
            .WithEpisodes(tvdbEpisode1, tvdbEpisode2)
            .Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBeNull();
    }

    [Fact]
    public async Task SkipsFallbackWhenEpisodeTitleCannotBeParsed()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Mynd", "", DateTime.UtcNow);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBeNull();
    }

    [Fact]
    public async Task FallbackOnlyProcessesUnmatchedEpisodes()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow);
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "þáttur 2", "", DateTime.UtcNow);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode101 = new TvdbEpisodeDataBuilder()
            .WithId(101)
            .WithSeasonNumber(1)
            .WithNumber(1)
            .WithNameTranslations("isl")
            .Build();
        Episode tvdbEpisode102 = new TvdbEpisodeDataBuilder()
            .WithId(102)
            .WithSeasonNumber(1)
            .WithNumber(2)
            .Build();
        Episode tvdbEpisode202 = new TvdbEpisodeDataBuilder()
            .WithId(202)
            .WithSeasonNumber(2)
            .WithNumber(2)
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000)
            .WithEpisodes(tvdbEpisode101, tvdbEpisode102, tvdbEpisode202)
            .Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 1", "", "isl", true));
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBe(101);
        program.Episodes[1].TvdbId.ShouldBe(202);
    }

    [Fact]
    public async Task SkipsFallbackWhenOtherSeasonHasSameEpisodeCount()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        Episode tvdbEpisode4 = new TvdbEpisodeDataBuilder().WithId(401).WithSeasonNumber(4).WithNumber(1).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000)
            .WithEpisodes(tvdbEpisode2, tvdbEpisode4)
            .Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBeNull();
    }

    [Fact]
    public async Task IntegerSeasonSuffixUsedInFallback()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show 3").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(301).WithSeasonNumber(3).WithNumber(1).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBe(301);
        program.Episodes[0].SeasonNumber.ShouldBe(3);
    }
}
