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
using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvProgramRefreshJobTests;

public sealed class RecordsEnqueueBatch
{
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
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

    private RuvProgramRefreshJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<RuvProgramRefreshJob>.Instance,
        _ruv,
        dbContext,
        _settingsStore,
        _syncQueue,
        _tvdbLookupQueue,
        new DomainEventBroadcaster());

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
        await sut.Execute(_context);

        // Assert
        _syncQueue.LastEnqueuedCount.ShouldBe(2);
    }

    [Fact]
    public async Task WhenProgramsEnqueued_SetsLastEnqueuedAt()
    {
        // Arrange
        DateTimeOffset before = DateTimeOffset.UtcNow;
        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(1)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        DateTimeOffset after = DateTimeOffset.UtcNow;
        _syncQueue.LastEnqueuedAt.ShouldNotBeNull();
        _syncQueue.LastEnqueuedAt.Value.ShouldBeInRange(before, after);
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
        await sut.Execute(_context);

        // Assert — 2 from API + 1 known = 3 total enqueued
        _syncQueue.LastEnqueuedCount.ShouldBe(3);
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
        await sut.Execute(_context);

        // Assert
        _syncQueue.LastEnqueuedCount.ShouldBe(0);
    }
}
