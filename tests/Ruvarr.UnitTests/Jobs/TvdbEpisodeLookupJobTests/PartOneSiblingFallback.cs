using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.TvdbEpisodeLookup.Jobs;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbEpisodeLookupJobTests;

public sealed class PartOneSiblingFallback
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly TvdbEpisodeLookupNotifier _notifier = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public PartOneSiblingFallback()
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
        dbContext, _tvdb, _sonarr, _notifier, new DomainEventBroadcaster());

    [Fact]
    public async Task MatchesPartTwoWhenPartOneIsMached()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar, fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar, síðari hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(501)
            .WithName("Christmas Lads Part 1")
            .WithSeasonNumber(1)
            .WithNumber(3)
            .WithNameTranslations("isl")
            .Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(502)
            .WithName("Christmas Lads Part 2")
            .WithSeasonNumber(1)
            .WithNumber(4)
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000)
            .WithEpisodes(tvdbEpisode1, tvdbEpisode2)
            .Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(501, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Jólasveinar, fyrri hluti", "", "isl", true));
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBe(501);
        program.Episodes[0].SeasonNumber.ShouldBe(1);
        program.Episodes[0].EpisodeNumber.ShouldBe(3);
        program.Episodes[1].TvdbId.ShouldBe(502);
        program.Episodes[1].SeasonNumber.ShouldBe(1);
        program.Episodes[1].EpisodeNumber.ShouldBe(4);
    }

    [Fact]
    public async Task MatchesPartTwoWithDashSeparator()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar - fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar - síðari hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(601)
            .WithName("Christmas Lads (1)")
            .WithSeasonNumber(2)
            .WithNumber(5)
            .WithNameTranslations("isl")
            .Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(602)
            .WithName("Christmas Lads (2)")
            .WithSeasonNumber(2)
            .WithNumber(6)
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000)
            .WithEpisodes(tvdbEpisode1, tvdbEpisode2)
            .Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(601, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Jólasveinar - fyrri hluti", "", "isl", true));
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBe(601);
        program.Episodes[1].TvdbId.ShouldBe(602);
        program.Episodes[1].SeasonNumber.ShouldBe(2);
        program.Episodes[1].EpisodeNumber.ShouldBe(6);
    }

    [Fact]
    public async Task SkipsWhenPartOneSiblingIsNotMatched()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar, fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar, síðari hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
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
        program.Episodes[1].TvdbId.ShouldBeNull();
    }

    [Fact]
    public async Task SkipsWhenNoCorrespondingTvdbPartTwoEpisodeExists()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar, fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar, síðari hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // TVDB part 1 episode has "Part 1" suffix but no corresponding "Part 2" episode exists
        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(501)
            .WithName("Christmas Lads Part 1")
            .WithSeasonNumber(1)
            .WithNumber(3)
            .WithNameTranslations("isl")
            .Build();
        Episode tvdbEpisodeUnrelated = new TvdbEpisodeDataBuilder()
            .WithId(503)
            .WithName("Something Else")
            .WithSeasonNumber(1)
            .WithNumber(4)
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000)
            .WithEpisodes(tvdbEpisode1, tvdbEpisodeUnrelated)
            .Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(501, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Jólasveinar, fyrri hluti", "", "isl", true));
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBe(501);
        program.Episodes[1].TvdbId.ShouldBeNull();
    }

    [Fact]
    public async Task SkipsWhenTvdbPartOneEpisodeHasNoPartSuffix()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar, fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar, síðari hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // TVDB part 1 episode does NOT have a "Part 1" or "(1)" suffix
        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(501)
            .WithName("Christmas Lads")
            .WithSeasonNumber(1)
            .WithNumber(3)
            .WithNameTranslations("isl")
            .Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(502)
            .WithName("Christmas Lads Continued")
            .WithSeasonNumber(1)
            .WithNumber(4)
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000)
            .WithEpisodes(tvdbEpisode1, tvdbEpisode2)
            .Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(501, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Jólasveinar, fyrri hluti", "", "isl", true));
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBe(501);
        program.Episodes[1].TvdbId.ShouldBeNull();
    }

    [Fact]
    public async Task MatchesPartTwoWithSeinniHluti()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar, fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar, seinni hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(501)
            .WithName("Christmas Lads Part 1")
            .WithSeasonNumber(1)
            .WithNumber(3)
            .WithNameTranslations("isl")
            .Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(502)
            .WithName("Christmas Lads Part 2")
            .WithSeasonNumber(1)
            .WithNumber(4)
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000)
            .WithEpisodes(tvdbEpisode1, tvdbEpisode2)
            .Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _tvdb.GetEpisodeTranslationAsync(501, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Jólasveinar, fyrri hluti", "", "isl", true));
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].TvdbId.ShouldBe(501);
        program.Episodes[1].TvdbId.ShouldBe(502);
        program.Episodes[1].SeasonNumber.ShouldBe(1);
        program.Episodes[1].EpisodeNumber.ShouldBe(4);
    }

    [Fact]
    public async Task SkipsNonPartTwoEpisodes()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Regular Episode", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
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
    }
}
