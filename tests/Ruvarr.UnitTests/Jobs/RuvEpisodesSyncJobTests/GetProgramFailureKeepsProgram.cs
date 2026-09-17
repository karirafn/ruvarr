using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Jobs;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvEpisodesSyncJobTests;

public sealed class GetProgramFailureKeepsProgram
{
    private const int RuvProgramId = 77;
    private const string ExistingEpisodeId = "ep-existing";

    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public GetProgramFailureKeepsProgram()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key"));
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<Series>>(Array.Empty<Series>()));
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MissingEpisode>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private RuvEpisodesSyncJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<RuvEpisodesSyncJob>.Instance,
        _ruv, dbContext, _sonarr, _syncQueue, new DomainEventBroadcaster(), _settingsStore);

    [Fact]
    public async Task WhenGetProgramAsyncReturnsRequestFailure_ProgramAndEpisodesUnchanged()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using RuvarrDbContext dbContext = CreateDbContext();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(RuvProgramId)
            .WithMultipleEpisodes()
            .Build();

        program.TryAddEpisode(
            id: ExistingEpisodeId,
            uri: new Uri("http://ruv.is/ep1"),
            title: "Episode 1",
            description: "First",
            firstRun: DateTime.UtcNow,
            duration: TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns(new Result<RuvTvProgram>(ApiClientErrors.RequestFailed));

        _syncQueue.Enqueue(RuvProgramId, "Test Program");

        RuvEpisodesSyncJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert — queue drained
        _syncQueue.Items.ShouldBeEmpty();

        // Assert — program and episode still in database
        List<RuvProgram> programs = await dbContext.Set<RuvProgram>().ToListAsync(cancellationToken);
        List<RuvEpisode> episodes = await dbContext.Set<RuvEpisode>().ToListAsync(cancellationToken);

        programs.ShouldHaveSingleItem();
        episodes.ShouldHaveSingleItem();
        episodes[0].RuvId.ShouldBe(ExistingEpisodeId);
    }

    [Fact]
    public async Task WhenGetProgramAsyncReturnsRequestFailure_SecondProgramInSameBatchStillProcesses()
    {
        // Arrange
        const int Program2RuvId = 78;
        const string Program2EpisodeId = "ep-p2";
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using RuvarrDbContext dbContext = CreateDbContext();

        RuvProgram program1 = new RuvProgramBuilder()
            .WithRuvId(RuvProgramId)
            .WithName("Program One")
            .WithMultipleEpisodes()
            .Build();

        RuvProgram program2 = new RuvProgramBuilder()
            .WithRuvId(Program2RuvId)
            .WithName("Program Two")
            .WithMultipleEpisodes()
            .Build();

        dbContext.Set<RuvProgram>().Add(program1);
        dbContext.Set<RuvProgram>().Add(program2);
        await dbContext.SaveChangesAsync(cancellationToken);

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns(new Result<RuvTvProgram>(ApiClientErrors.RequestFailed));

        RuvTvProgram program2ApiResponse = CreateRuvTvProgram(
            Program2RuvId,
            "Program Two",
            [CreateRuvTvEpisode(Program2RuvId, Program2EpisodeId, "Episode 1")]);

        _ruv.GetProgramAsync(Program2RuvId, Arg.Any<CancellationToken>())
            .Returns(new Result<RuvTvProgram>(program2ApiResponse));

        _syncQueue.Enqueue(RuvProgramId, "Program One");
        _syncQueue.Enqueue(Program2RuvId, "Program Two");

        RuvEpisodesSyncJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert — queue fully drained
        _syncQueue.Items.ShouldBeEmpty();

        // Assert — program2's episode persisted
        List<RuvEpisode> episodes = await dbContext.Set<RuvEpisode>().ToListAsync(cancellationToken);
        episodes.ShouldContain(e => e.RuvId == Program2EpisodeId);
    }

    private static RuvTvEpisode CreateRuvTvEpisode(int seriesId, string id, string title) => new(
        Id: id,
        Number: 1,
        SeriesId: seriesId,
        FirstRun: DateTime.UtcNow,
        FileExpires: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        Rating: 0,
        Duration: 1800,
        DurationFriendly: "30 min",
        Event: 0,
        Title: title,
        Slug: "test-episode",
        Description: ["Test description"],
        ImageRenditions: new RuvImageRenditions([]),
        Image: new Uri("http://ruv.is/image.jpg"),
        ImageOg: new Uri("http://ruv.is/image-og.jpg"),
        Scope: "ruv",
        SubtitlesUrl: new Uri("http://ruv.is/subs.vtt"),
        Subtitles: new RuvSubtitles(new Uri("http://ruv.is/subs-is.vtt")),
        OpenSubtitles: false,
        ClosedSubtitles: false,
        AutoSubtitles: false,
        File: new Uri("http://ruv.is/stream.m3u8"),
        Temp: new RuvTemp("file.mp4", "folder"),
        Clips: [],
        Files: new RuvFiles(new RuvVodmp4("file.mp4", "folder", "file", false, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), 0, "0", "mp4", "ruv")),
        CreditPoint: 0);

    private static RuvTvProgram CreateRuvTvProgram(int id, string name, IReadOnlyList<RuvTvEpisode> episodes) => new(
        LastUpdated: DateTimeOffset.UtcNow,
        Id: id,
        Title: name,
        ForeignTitle: "Test",
        Slug: "test-program",
        ImageRenditions: new RuvImageRenditions([]),
        Image: null,
        ImageOg: new Uri("http://ruv.is/og.jpg"),
        PortraitImageRenditions: new RuvPortraitImageRenditions([]),
        PortraitImage: new Uri("http://ruv.is/portrait.jpg"),
        Description: ["Test description"],
        Format: "tv",
        Categories: [],
        Division: "test",
        MultipleEpisodes: true,
        Episodes: episodes,
        ReverseEpisodeOrder: false,
        WebAvailableEpisodes: episodes.Count,
        VodAvailableEpisodes: episodes.Count,
        PodcastVailableEpisodes: 0,
        WebLatestDate: DateTime.UtcNow,
        Channel: "ruv",
        WebPlayerUrl: new Uri("http://ruv.is/player"));
}
