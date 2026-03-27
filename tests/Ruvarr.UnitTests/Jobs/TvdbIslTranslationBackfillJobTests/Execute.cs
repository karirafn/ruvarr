using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Jobs;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbIslTranslationBackfillJobTests;

public sealed class Execute
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public Execute()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _context.CancellationToken.Returns(CancellationToken.None);
        _settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: "test-key"));
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private TvdbIslTranslationBackfillJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<TvdbIslTranslationBackfillJob>.Instance,
        dbContext,
        _tvdb,
        _settingsStore);

    [Fact]
    public async Task SkipsWhenBackfillAlreadyComplete()
    {
        // Arrange
        _settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: "test-key") { IslTranslationBackfillComplete = true });
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbIslTranslationBackfillJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        _ = _tvdb.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipsWhenTvdbNotConfigured()
    {
        // Arrange
        _settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: ""));
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbIslTranslationBackfillJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        _ = _tvdb.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetsBackfillCompleteFlag()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbIslTranslationBackfillJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        await _settingsStore.Received(1).SaveAsync(
            Arg.Is<RuvarrSettings>(s => s.IslTranslationBackfillComplete),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatesHasIslTranslationFromTvdbData()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        RuvProgram program = new RuvProgramBuilder().Build();
        program.MatchTvdb(TvdbSeries.Create(id: 42, name: "Test Series", slug: "test-series"));
        dbContext.Set<RuvProgram>().Add(program);

        RuvEpisode episode = RuvEpisode.Create(program, "ep1", new Uri("http://test.com"), "Ep 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        episode.Match(tvdbId: 100, season: 1, episode: 1, isMissing: false, hasIslTranslation: false);
        episode.ClearDomainEvents();
        dbContext.Set<RuvEpisode>().Add(episode);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode = CreateTvdbEpisode(id: 100, seasonNumber: 1, number: 1, nameTranslations: ["eng", "isl"]);

        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(42)
            .WithEpisodes(tvdbEpisode)
            .Build();

        _tvdb.GetSeriesAsync(42, Arg.Any<CancellationToken>())
            .Returns(seriesData);

        TvdbIslTranslationBackfillJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        TvdbEpisode updated = await dbContext.Set<TvdbEpisode>().FirstAsync(e => e.TvdbId == 100, TestContext.Current.CancellationToken);
        updated.HasIslTranslation.ShouldBeTrue();
    }

    [Fact]
    public async Task DoesNotUpdateEpisodeWithoutIslTranslation()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        RuvProgram program = new RuvProgramBuilder().Build();
        program.MatchTvdb(TvdbSeries.Create(id: 42, name: "Test Series", slug: "test-series"));
        dbContext.Set<RuvProgram>().Add(program);

        RuvEpisode episode = RuvEpisode.Create(program, "ep1", new Uri("http://test.com"), "Ep 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        episode.Match(tvdbId: 100, season: 1, episode: 1, isMissing: false, hasIslTranslation: false);
        episode.ClearDomainEvents();
        dbContext.Set<RuvEpisode>().Add(episode);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Episode tvdbEpisode = CreateTvdbEpisode(id: 100, seasonNumber: 1, number: 1, nameTranslations: ["eng"]);

        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(42)
            .WithEpisodes(tvdbEpisode)
            .Build();

        _tvdb.GetSeriesAsync(42, Arg.Any<CancellationToken>())
            .Returns(seriesData);

        TvdbIslTranslationBackfillJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        TvdbEpisode updated = await dbContext.Set<TvdbEpisode>().FirstAsync(e => e.TvdbId == 100, TestContext.Current.CancellationToken);
        updated.HasIslTranslation.ShouldBeFalse();
    }

    private static Episode CreateTvdbEpisode(int id, int seasonNumber, int number, IReadOnlyList<string> nameTranslations) => new(
        Id: id,
        SeriesId: 42,
        Name: $"Episode {number}",
        Overview: "",
        Aired: "2025-01-01",
        Runtime: 30,
        NameTranslations: nameTranslations,
        OverviewTranslations: [],
        Image: new Uri("http://example.com/img.jpg"),
        ImageType: 1,
        IsMovie: 0,
        Number: number,
        AbsoluteNumber: number,
        SeasonNumber: seasonNumber,
        LastUpdated: "2025-01-01",
        FinaleType: null,
        Year: "2025",
        AirsAfterSeason: 0,
        AirsBeforeSeason: 0,
        AirsBeforeEpisode: 0);
}
