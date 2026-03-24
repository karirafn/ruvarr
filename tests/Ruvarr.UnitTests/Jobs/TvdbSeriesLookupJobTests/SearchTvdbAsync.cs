using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;
using Ruvarr.TvdbSeriesLookup.Jobs;
using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbSeriesLookupJobTests;

public sealed class SearchTvdbAsync
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly TvdbSeriesLookupNotifier _lookupQueue = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public SearchTvdbAsync()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _context.CancellationToken.Returns(CancellationToken.None);
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            TvdbApiKey: "tvdb-key", TmdbApiKey: "tmdb-key"));
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private TvdbSeriesLookupJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<TvdbSeriesLookupJob>.Instance,
        dbContext,
        _tvdb,
        _lookupQueue,
        new DomainEventBroadcaster(),
        _settingsStore);

    private static SearchResponse CreateSearchResponse(params Datum[] data) => new(
        Status: "success",
        Data: data,
        Links: new Links(
            Prev: null,
            Self: new Uri("http://test.com"),
            Next: null,
            TotalItems: data.Length,
            PageSize: 100));

    [Fact]
    public async Task ReturnsNull_WhenSingleMatch_AndYearSuffixedVariantExistsInName()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName(null)
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum exactMatch = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("100")
            .Build();
        Datum yearVariant = new DatumBuilder()
            .WithName("Katla (2021)")
            .WithTvdbId("200")
            .Build();

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSearchResponse(exactMatch, yearVariant));
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldBeNull();
        program.NextLookup.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenSingleMatch_AndYearSuffixedVariantExistsInIcelandicTranslation()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName(null)
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum exactMatch = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("100")
            .WithTranslations(new Dictionary<string, string> { ["isl"] = "Katla" })
            .Build();
        Datum yearVariant = new DatumBuilder()
            .WithName("Other Name")
            .WithTvdbId("200")
            .WithTranslations(new Dictionary<string, string> { ["isl"] = "Katla (2021)" })
            .Build();

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSearchResponse(exactMatch, yearVariant));
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldBeNull();
        program.NextLookup.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReturnsMatch_WhenSingleMatch_AndNoYearSuffixedVariantExists()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName(null)
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum exactMatch = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("100")
            .Build();
        Datum otherSeries = new DatumBuilder()
            .WithName("Something Else")
            .WithTvdbId("200")
            .Build();

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSearchResponse(exactMatch, otherSeries));
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldNotBeNull();
        program.Series.TvdbId.ShouldBe(100);
    }

    [Fact]
    public async Task ReturnsNull_WhenMultipleExactMatches_RegardlessOfYearSuffix()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName(null)
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum match1 = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("100")
            .Build();
        Datum match2 = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("200")
            .Build();

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSearchResponse(match1, match2));
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenZeroMatches_RegardlessOfYearSuffix()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName(null)
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum unrelated = new DatumBuilder()
            .WithName("Something Else")
            .WithTvdbId("100")
            .Build();

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSearchResponse(unrelated));
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldBeNull();
    }

    [Fact]
    public async Task DoesNotFalsePositive_WhenYearSuffixedEntryBaseNameDoesNotMatch()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName(null)
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum exactMatch = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("100")
            .Build();
        Datum differentYearVariant = new DatumBuilder()
            .WithName("Other Show (2021)")
            .WithTvdbId("200")
            .Build();

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSearchResponse(exactMatch, differentYearVariant));
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldNotBeNull();
        program.Series.TvdbId.ShouldBe(100);
    }

    [Fact]
    public async Task DisambiguatesByEpisodeTranslation_WhenMultipleExactMatches_AndSingleCandidateMatchesEpisode()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName(null)
            .Build();
        program.TryAddEpisode("ep1", new Uri("http://test.com"), "Eldur", "desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum match1 = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("100")
            .Build();
        Datum match2 = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("200")
            .Build();

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSearchResponse(match1, match2));

        Episode tvdbEp1 = new TvdbEpisodeDataBuilder()
            .WithId(10)
            .WithName("Fire")
            .WithNameTranslations("isl")
            .WithSeasonNumber(1)
            .WithNumber(1)
            .Build();
        SeriesData series100 = new TvdbSeriesDataBuilder()
            .WithId(100)
            .WithName("Katla")
            .WithEpisodes(tvdbEp1)
            .Build();
        _tvdb.GetSeriesAsync(100, Arg.Any<CancellationToken>())
            .Returns(series100);
        _tvdb.GetEpisodeTranslationAsync(10, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Eldur", "", "isl", false));

        Episode tvdbEp2 = new TvdbEpisodeDataBuilder()
            .WithId(20)
            .WithName("Unrelated")
            .WithNameTranslations("isl")
            .WithSeasonNumber(1)
            .WithNumber(1)
            .Build();
        SeriesData series200 = new TvdbSeriesDataBuilder()
            .WithId(200)
            .WithName("Katla")
            .WithEpisodes(tvdbEp2)
            .Build();
        _tvdb.GetSeriesAsync(200, Arg.Any<CancellationToken>())
            .Returns(series200);
        _tvdb.GetEpisodeTranslationAsync(20, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Ótengdur", "", "isl", false));

        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldNotBeNull();
        program.Series.TvdbId.ShouldBe(100);
    }

    [Fact]
    public async Task ReturnsNull_WhenMultipleExactMatches_AndMultipleCandidatesMatchEpisodes()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName(null)
            .Build();
        program.TryAddEpisode("ep1", new Uri("http://test.com"), "Eldur", "desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum match1 = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("100")
            .Build();
        Datum match2 = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("200")
            .Build();

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSearchResponse(match1, match2));

        Episode tvdbEp1 = new TvdbEpisodeDataBuilder()
            .WithId(10)
            .WithName("Fire")
            .WithNameTranslations("isl")
            .WithSeasonNumber(1)
            .WithNumber(1)
            .Build();
        SeriesData series100 = new TvdbSeriesDataBuilder()
            .WithId(100)
            .WithName("Katla")
            .WithEpisodes(tvdbEp1)
            .Build();
        _tvdb.GetSeriesAsync(100, Arg.Any<CancellationToken>())
            .Returns(series100);
        _tvdb.GetEpisodeTranslationAsync(10, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Eldur", "", "isl", false));

        Episode tvdbEp2 = new TvdbEpisodeDataBuilder()
            .WithId(20)
            .WithName("Also Fire")
            .WithNameTranslations("isl")
            .WithSeasonNumber(1)
            .WithNumber(1)
            .Build();
        SeriesData series200 = new TvdbSeriesDataBuilder()
            .WithId(200)
            .WithName("Katla")
            .WithEpisodes(tvdbEp2)
            .Build();
        _tvdb.GetSeriesAsync(200, Arg.Any<CancellationToken>())
            .Returns(series200);
        _tvdb.GetEpisodeTranslationAsync(20, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Eldur", "", "isl", false));

        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenMultipleExactMatches_AndNoTranslationsAvailable()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName(null)
            .Build();
        program.TryAddEpisode("ep1", new Uri("http://test.com"), "Eldur", "desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum match1 = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("100")
            .Build();
        Datum match2 = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("200")
            .Build();

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSearchResponse(match1, match2));

        SeriesData series100 = new TvdbSeriesDataBuilder()
            .WithId(100)
            .WithName("Katla")
            .Build();
        _tvdb.GetSeriesAsync(100, Arg.Any<CancellationToken>())
            .Returns(series100);

        SeriesData series200 = new TvdbSeriesDataBuilder()
            .WithId(200)
            .WithName("Katla")
            .Build();
        _tvdb.GetSeriesAsync(200, Arg.Any<CancellationToken>())
            .Returns(series200);

        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldBeNull();
    }

    [Fact]
    public async Task SkipsDisambiguation_WhenMultipleExactMatches_AndProgramHasNoEpisodes()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName(null)
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum match1 = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("100")
            .Build();
        Datum match2 = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("200")
            .Build();

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSearchResponse(match1, match2));
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldBeNull();
        await _tvdb.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FallsBackToForeignName_WhenIcelandicNameIsAmbiguous()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Katla")
            .WithForeignName("Unique Foreign Name")
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Datum exactMatch = new DatumBuilder()
            .WithName("Katla")
            .WithTvdbId("100")
            .WithTranslations(new Dictionary<string, string> { ["isl"] = "Katla" })
            .Build();
        Datum yearVariant = new DatumBuilder()
            .WithName("Katla (2021)")
            .WithTvdbId("200")
            .Build();

        SearchResponse ambiguousResponse = CreateSearchResponse(exactMatch, yearVariant);
        SearchResponse foreignResponse = CreateSearchResponse(
            new DatumBuilder()
                .WithName("Unique Foreign Name")
                .WithTvdbId("300")
                .Build());

        _tvdb.SearchAsync(Arg.Is<string>(q => q.Contains("KATLA")), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ambiguousResponse);
        _tvdb.SearchAsync(Arg.Is<string>(q => q.Contains("UNIQUE")), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(foreignResponse);
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.Series.ShouldNotBeNull();
        program.Series.TvdbId.ShouldBe(300);
    }
}
