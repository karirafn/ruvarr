using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;
using Ruvarr.Testing.Logging;
using Ruvarr.TvdbEpisodeLookup;
using Ruvarr.TvdbEpisodeLookup.Jobs;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.TvdbEpisodeLookup.Strategies;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbEpisodeLookupJobTests;

public sealed class MatchRecheck : IDisposable
{
    private const int SeriesId = 1000;

    // Gettu betur's regular seasons on TVDB: no 1999 or 2000.
    private static readonly int[] GettuBeturSeasons =
        [.. Enumerable.Range(1987, 12), 2003, 2004, 2005, .. Enumerable.Range(2022, 5)];

    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly TvdbEpisodeLookupNotifier _notifier = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly RecordingLogger<TvdbEpisodeLookupJob> _logger = new();
    private readonly RuvarrDbContext _dbContext;
    private readonly List<Episode> _tvdbEpisodes = [];

    public MatchRecheck()
    {
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<MissingEpisode>());
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Ruvarr.Infrastructure.Sonarr.Models.Series>());
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            TvdbApiKey: "tvdb-key", TmdbApiKey: "tmdb-key"));
        _tvdb.GetSeriesAsync(SeriesId, Arg.Any<CancellationToken>())
            .Returns(_ => new TvdbSeriesDataBuilder().WithId(SeriesId).WithEpisodes([.. _tvdbEpisodes]).Build());
        _dbContext = new RuvarrDbContext(
            new DbContextOptionsBuilder<RuvarrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            _serviceProvider);
    }

    public void Dispose() => _dbContext.Dispose();

    private TvdbEpisodeLookupJob CreateJob(ITvdbEpisodeMatcher? matcher = null) => new(
        _logger,
        _dbContext, _tvdb, _sonarr, _notifier, new DomainEventBroadcaster(), _settingsStore,
        matcher ?? new TvdbEpisodeMatcher(
            [
                new TranslationMatchingStrategy(_tvdb, NullLogger<TranslationMatchingStrategy>.Instance),
                new EpisodeNumberMatchingStrategy(NullLogger<EpisodeNumberMatchingStrategy>.Instance),
                new PartOneSiblingMatchingStrategy(NullLogger<PartOneSiblingMatchingStrategy>.Instance),
            ],
            NullLogger<TvdbEpisodeMatcher>.Instance));

    private void TvdbEpisode(int id, int season, int number, string? islName = null)
    {
        TvdbEpisodeDataBuilder builder = new TvdbEpisodeDataBuilder().WithId(id).WithSeasonNumber(season).WithNumber(number);
        if (islName is not null)
        {
            builder.WithNameTranslations("isl");
            _tvdb.GetEpisodeTranslationAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new EpisodeTranslation(islName, "", "isl", true));
        }

        _tvdbEpisodes.Add(builder.Build());
    }

    private void SeasonsWithoutTheTitles(params int[] seasons)
    {
        foreach (int season in seasons)
        {
            TvdbEpisode(id: (season * 100) + 99, season, number: 99, islName: $"Annar þáttur {season}");
        }
    }

    private static RuvProgram Program(string name, params string[] titles)
    {
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName(name).WithMultipleEpisodes().Build();
        for (int i = 0; i < titles.Length; i++)
        {
            program.TryAddEpisode($"ep{i:D4}", new Uri("http://test.com"), titles[i], "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        }

        program.MatchTvdb(new TvdbSeriesBuilder().WithId(SeriesId).Build());
        return program;
    }

    private static void Link(RuvEpisode episode, int tvdbId, int season, int number, MatchOrigin origin = MatchOrigin.Automatic) =>
        episode.Match(tvdbId, season, number, SonarrStatuses.NotTracked, origin, hasIslTranslation: true);

    private async Task RefreshAsync(RuvProgram program, bool requestRefresh = true)
    {
        if (requestRefresh)
        {
            program.RequestRefresh();
        }

        program.ClearDomainEvents();
        _dbContext.Set<RuvProgram>().Add(program);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _notifier.Enqueue(program.RuvId, program.Name);

        await CreateJob().Execute(_context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task YearProgramMissingFromTvdb_EndsFullyUnmatched()
    {
        // Arrange
        SeasonsWithoutTheTitles(GettuBeturSeasons);
        TvdbEpisode(2024_05, 2024, 5, "MR - Versló");
        TvdbEpisode(2023_02, 2023, 2, "MH - FSu");
        RuvProgram program = Program("Gettu betur 1999", "MR - Versló", "MH - FSu");
        Link(program.Episodes[0], 2024_05, 2024, 5);
        Link(program.Episodes[1], 2023_02, 2023, 2);

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes.ShouldAllBe(x => x.TvdbEpisodes.Count == 0 && x.MatchOrigin == null);
    }

    [Fact]
    public async Task YearProgram_RelinksAnOffYearLinkToItsOwnYearInTheSamePass()
    {
        // Arrange
        SeasonsWithoutTheTitles(GettuBeturSeasons);
        TvdbEpisode(1998_03, 1998, 3, "MR - Versló");
        TvdbEpisode(2024_05, 2024, 5, "MR - Versló");
        RuvProgram program = Program("Gettu betur 1998", "MR - Versló");
        Link(program.Episodes[0], 2024_05, 2024, 5);

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(1998_03);
    }

    [Fact]
    public async Task YearProgram_KeepsLinksInItsOwnYear()
    {
        // Arrange
        SeasonsWithoutTheTitles(GettuBeturSeasons);
        TvdbEpisode(1988_01, 1988, 1, "MR - MH");
        TvdbEpisode(2024_01, 2024, 1, "MR - MH");
        RuvProgram program = Program("Gettu betur 1988", "MR - MH");
        Link(program.Episodes[0], 1988_01, 1988, 1);

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(1988_01);
    }

    [Fact]
    public async Task PreferredSeasonCollision_IsRelinkedToTheDeclaredSeason()
    {
        // Arrange
        TvdbEpisode(301, 3, 1, "Þáttur 1 af 6");
        TvdbEpisode(401, 4, 1, "Þáttur 1 af 6");
        RuvProgram program = Program("Í garðinum með Gurrý IV", "Þáttur 1 af 6");
        Link(program.Episodes[0], 301, 3, 1);

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(401);
        program.Episodes[0].MatchOrigin.ShouldBe(MatchOrigin.Automatic);
    }

    [Theory]
    [InlineData("Andri og Edda I", 3)]
    [InlineData("Andri og Edda II", 3)]
    [InlineData("Kúpull 16", 1)]
    [InlineData("Söngvakeppnin 2024", 32)]
    [InlineData("Bursti I", 2)]
    [InlineData("Lalli II", 1)]
    [InlineData("Íslendingar", 2)]
    public async Task LegitimateOffSeasonLink_IsKept(string programName, int linkedSeason)
    {
        // Arrange
        SeasonsWithoutTheTitles(1, 2, 3, 31, 32);
        TvdbEpisode((linkedSeason * 100) + 5, linkedSeason, 5, "Á ferð");
        RuvProgram program = Program(programName, "Á ferð");
        Link(program.Episodes[0], (linkedSeason * 100) + 5, linkedSeason, 5);

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe((linkedSeason * 100) + 5);
    }

    [Fact]
    public async Task AmbiguousLinkWithoutADeclaredSeason_IsKept()
    {
        // Arrange
        TvdbEpisode(1990_02, 1990, 2, "MR - FSu");
        TvdbEpisode(2026_05, 2026, 5, "MR - FSu");
        RuvProgram program = Program("Gettu betur", "MR - FSu");
        Link(program.Episodes[0], 2026_05, 2026, 5);

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(2026_05);
    }

    [Fact]
    public async Task EpisodeNumberLinkWithoutATranslation_IsKept()
    {
        // Arrange
        TvdbEpisode(301, 3, 1);
        TvdbEpisode(401, 4, 1, "Eitthvað annað");
        RuvProgram program = Program("Þáttaröð IV", "Þáttur 1 af 6");
        Link(program.Episodes[0], 301, 3, 1);

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(301);
    }

    [Fact]
    public async Task ManualLink_IsNeverJudged()
    {
        // Arrange
        SeasonsWithoutTheTitles(GettuBeturSeasons);
        TvdbEpisode(2024_05, 2024, 5, "MR - Versló");
        RuvProgram program = Program("Gettu betur 1999", "MR - Versló");
        Link(program.Episodes[0], 2024_05, 2024, 5, MatchOrigin.Manual);

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(2024_05);
    }

    [Fact]
    public async Task WithoutARefreshRequest_LinksAreNotJudged()
    {
        // Arrange
        SeasonsWithoutTheTitles(GettuBeturSeasons);
        TvdbEpisode(2024_05, 2024, 5, "MR - Versló");
        RuvProgram program = Program("Gettu betur 1999", "MR - Versló");
        Link(program.Episodes[0], 2024_05, 2024, 5);

        // Act
        await RefreshAsync(program, requestRefresh: false);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem();
        await _tvdb.DidNotReceive().GetEpisodeTranslationAsync(2024_05, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearsTheRecheckRequestOnceDone()
    {
        // Arrange
        TvdbEpisode(101, 1, 1, "Þáttur 1");
        RuvProgram program = Program("Show", "Þáttur 1");

        // Act
        await RefreshAsync(program);

        // Assert
        _dbContext.ChangeTracker.Clear();
        RuvProgram stored = await _dbContext.Set<RuvProgram>().SingleAsync(TestContext.Current.CancellationToken);
        stored.MatchRecheckRequested.ShouldBeFalse();
    }

    [Fact]
    public async Task KeepsTheRecheckRequestWhenTvdbReturnsNoSeriesData()
    {
        // Arrange
        _tvdb.GetSeriesAsync(SeriesId, Arg.Any<CancellationToken>()).Returns((SeriesData?)null);
        RuvProgram program = Program("Gettu betur 1999", "MR - Versló");
        Link(program.Episodes[0], 2024_05, 2024, 5);

        // Act
        await RefreshAsync(program);

        // Assert
        _dbContext.ChangeTracker.Clear();
        RuvProgram stored = await _dbContext.Set<RuvProgram>()
            .Include(x => x.Episodes).ThenInclude(x => x.TvdbEpisodes)
            .SingleAsync(TestContext.Current.CancellationToken);
        stored.MatchRecheckRequested.ShouldBeTrue();
        stored.Episodes.ShouldHaveSingleItem().TvdbEpisodes.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunsTheStrategiesAfterUnmatching()
    {
        // Arrange
        SeasonsWithoutTheTitles(GettuBeturSeasons);
        TvdbEpisode(2024_05, 2024, 5, "MR - Versló");
        RuvProgram program = Program("Gettu betur 1999", "MR - Versló");
        Link(program.Episodes[0], 2024_05, 2024, 5);
        program.RequestRefresh();
        program.ClearDomainEvents();
        _dbContext.Set<RuvProgram>().Add(program);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _notifier.Enqueue(program.RuvId, program.Name);

        ITvdbEpisodeMatcher matcher = Substitute.For<ITvdbEpisodeMatcher>();
        int? linksSeenByMatcher = null;
        matcher.MatchAsync(Arg.Any<EpisodeMatchingContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => linksSeenByMatcher = call.Arg<EpisodeMatchingContext>().Program.Episodes[0].TvdbEpisodes.Count);

        // Act
        await CreateJob(matcher).Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        linksSeenByMatcher.ShouldBe(0);
    }

    [Fact]
    public async Task UnmatchLogsAWarningWithTheOldLinkAndDownloadStatus()
    {
        // Arrange
        SeasonsWithoutTheTitles(GettuBeturSeasons);
        TvdbEpisode(2024_05, 2024, 5, "MR - Versló");
        RuvProgram program = Program("Gettu betur 1999", "MR - Versló");
        Link(program.Episodes[0], 2024_05, 2024, 5);
        _dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(program.Episodes[0]));

        // Act
        await RefreshAsync(program);

        // Assert
        string message = _logger.Entries.Where(x => x.Level == LogLevel.Warning).ShouldHaveSingleItem().Message;
        message.ShouldContain("S2024E05");
        message.ShouldContain(nameof(DownloadQueueStatus.Pending));
    }

    [Fact]
    public async Task ManualSeasons_KeepLinksInEverySeasonOfTheSet()
    {
        // Arrange: Bursti I spans TVDB seasons 1 and 2
        TvdbEpisode(101, 1, 1, "Burstinn");
        TvdbEpisode(201, 2, 1, "Burstinn snýr aftur");
        TvdbEpisode(301, 3, 1, "Annar þáttur");
        RuvProgram program = Program("Bursti I", "Burstinn", "Burstinn snýr aftur");
        Link(program.Episodes[0], 101, 1, 1);
        Link(program.Episodes[1], 201, 2, 1);
        program.SetManualSeasons([1, 2], [1, 2, 3]).IsSuccess.ShouldBeTrue();

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(101);
        program.Episodes[1].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(201);
    }

    [Fact]
    public async Task ManualSeasons_RelinkAnAutomaticLinkOutsideTheSetToItsTitleInTheSet()
    {
        // Arrange
        TvdbEpisode(101, 1, 1, "Hús");
        TvdbEpisode(201, 2, 1, "Hús");
        RuvProgram program = Program("Ráðherrann", "Hús");
        Link(program.Episodes[0], 101, 1, 1);
        program.SetManualSeasons([2], [1, 2]).IsSuccess.ShouldBeTrue();

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(201);
    }

    [Fact]
    public async Task ManualSeasons_NeverTouchAManualLinkOutsideTheSet()
    {
        // Arrange
        TvdbEpisode(101, 1, 1, "Hús");
        TvdbEpisode(201, 2, 1, "Hús");
        RuvProgram program = Program("Ráðherrann", "Hús");
        Link(program.Episodes[0], 101, 1, 1, MatchOrigin.Manual);
        program.SetManualSeasons([2], [1, 2]).IsSuccess.ShouldBeTrue();

        // Act
        await RefreshAsync(program);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(101);
    }
}
