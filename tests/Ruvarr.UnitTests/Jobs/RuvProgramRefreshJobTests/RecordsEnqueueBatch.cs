using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.Jobs;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;
using Ruvarr.Testing.Time;
using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvProgramRefreshJobTests;

public sealed class RecordsEnqueueBatch
{
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ProgramRefreshNotifier _syncQueue = new(TimeProvider.System);
    private readonly TvdbSeriesLookupNotifier _tvdbLookupQueue = new();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public RecordsEnqueueBatch()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings { IgnoredChannels = [] });
        _ruv.GetKidsTvAsync(Arg.Any<CancellationToken>()).Returns((RuvFeaturedTv?)null);
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private RuvProgramRefreshJob CreateJob(RuvarrDbContext dbContext) =>
        CreateJob(dbContext, _syncQueue);

    private RuvProgramRefreshJob CreateJob(RuvarrDbContext dbContext, ProgramRefreshNotifier syncQueue) => new(
        NullLogger<RuvProgramRefreshJob>.Instance,
        _ruv,
        dbContext,
        _settingsStore,
        syncQueue,
        _tvdbLookupQueue,
        Substitute.For<IDomainEventBroadcaster>());

    private static RuvTvProgram CreateRuvTvProgram(int id, bool multipleEpisodes = true) => new(
        LastUpdated: DateTimeOffset.UtcNow,
        Id: id,
        Title: $"Program {id}",
        ForeignTitle: $"Foreign Program {id}",
        Slug: $"program-{id}",
        ImageRenditions: new RuvImageRenditions([]),
        Image: new Uri("http://example.com/image.jpg"),
        ImageOg: new Uri("http://example.com/image-og.jpg"),
        PortraitImageRenditions: new RuvPortraitImageRenditions([]),
        PortraitImage: new Uri("http://example.com/portrait.jpg"),
        Description: [],
        Format: "format",
        Categories: [],
        Division: "division",
        MultipleEpisodes: multipleEpisodes,
        Episodes: [],
        ReverseEpisodeOrder: false,
        WebAvailableEpisodes: 1,
        VodAvailableEpisodes: 0,
        PodcastVailableEpisodes: 0,
        WebLatestDate: DateTime.UtcNow,
        Channel: "ruv",
        WebPlayerUrl: new Uri("http://example.com/player"));

    [Fact]
    public async Task WhenProgramsEnqueued_SetsLastEnqueuedCount()
    {
        // Arrange
        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(1), CreateRuvTvProgram(2)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        _syncQueue.LastEnqueuedCount.ShouldBe(2);
    }

    [Fact]
    public async Task WhenProgramsEnqueued_SetsLastEnqueuedAt()
    {
        // Arrange
        DateTimeOffset fixedNow = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        ProgramRefreshNotifier syncQueue = new(new FixedTimeProvider(fixedNow));

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(1)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgramRefreshJob sut = CreateJob(dbContext, syncQueue);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        syncQueue.LastEnqueuedAt.ShouldBe(fixedNow);
    }

    [Fact]
    public async Task WhenApiAndKnownProgramsEnqueued_SumsTotal()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();

        // One known program not in API response
        RuvProgram knownProgram = new RuvProgramBuilder().WithRuvId(99).WithName("Known Show").WithMultipleEpisodes().Build();
        dbContext.Set<RuvProgram>().Add(knownProgram);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Two programs from the API
        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(1), CreateRuvTvProgram(2)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert — 2 from API + 1 known = 3 total enqueued
        _syncQueue.LastEnqueuedCount.ShouldBe(3);
    }

    [Fact]
    public async Task WhenProgramAppearsInBothApiAndKnownSet_CountsOnce()
    {
        // Arrange — program id 1 is in both the API response AND the known programs in the database.
        // EnqueueKnownProgramRefreshes excludes ids already fetched from the API via a SQL filter,
        // so the known-programs pass never sees id 1. The HashSet<int> distinct-count is a
        // defence-in-depth guarantee: even if that filter were removed, a program appearing in
        // both passes would still be counted once.
        using RuvarrDbContext dbContext = CreateDbContext();

        RuvProgram knownProgram = new RuvProgramBuilder().WithRuvId(1).WithName("Shared Show").WithMultipleEpisodes().Build();
        dbContext.Set<RuvProgram>().Add(knownProgram);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(1)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert — program id 1 was fed from both passes but counts as one distinct enqueue
        _syncQueue.LastEnqueuedCount.ShouldBe(1);
    }

    [Fact]
    public async Task WhenNoMultiEpisodePrograms_SetsCountToZero()
    {
        // Arrange
        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(1, multipleEpisodes: false)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        _syncQueue.LastEnqueuedCount.ShouldBe(0);
    }
}
