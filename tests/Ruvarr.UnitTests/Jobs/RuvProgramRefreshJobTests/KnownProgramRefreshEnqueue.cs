using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.Jobs;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.TvdbSeriesLookup.Notifiers;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvProgramRefreshJobTests;

public sealed class KnownProgramRefreshEnqueue
{
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
    private readonly TvdbSeriesLookupNotifier _tvdbLookupQueue = new();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public KnownProgramRefreshEnqueue()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings { IgnoredChannels = [] });
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
    public async Task EnqueuesDbProgram_NotInApiResponse_WhenHasMultipleEpisodesIsTrue()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram knownProgram = new RuvProgramBuilder().WithRuvId(99).WithName("Known Show").WithMultipleEpisodes().Build();
        dbContext.Set<RuvProgram>().Add(knownProgram);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(id: 1)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);
        _ruv.GetKidsTvAsync(Arg.Any<CancellationToken>()).Returns((RuvFeaturedTv?)null);

        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _syncQueue.Items.ShouldContain(x => x.RuvId == 99);
    }

    [Fact]
    public async Task DoesNotDoubleEnqueue_WhenProgramIsInApiResponse()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram existingProgram = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        dbContext.Set<RuvProgram>().Add(existingProgram);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(id: 1)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);
        _ruv.GetKidsTvAsync(Arg.Any<CancellationToken>()).Returns((RuvFeaturedTv?)null);

        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert — program 1 should appear exactly once in the queue
        _syncQueue.Items.Count(x => x.RuvId == 1).ShouldBe(1);
    }

    [Fact]
    public async Task SkipsPrograms_WhenHasMultipleEpisodesIsFalse()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram singleEpisodeProgram = new RuvProgramBuilder().WithRuvId(99).WithName("Single Episode").WithMultipleEpisodes(false).Build();
        dbContext.Set<RuvProgram>().Add(singleEpisodeProgram);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(id: 1)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);
        _ruv.GetKidsTvAsync(Arg.Any<CancellationToken>()).Returns((RuvFeaturedTv?)null);

        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _syncQueue.Items.ShouldNotContain(x => x.RuvId == 99);
    }

    [Fact]
    public async Task EnqueuesMultipleKnownPrograms()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram known1 = new RuvProgramBuilder().WithRuvId(90).WithName("Known A").WithMultipleEpisodes().Build();
        RuvProgram known2 = new RuvProgramBuilder().WithRuvId(91).WithName("Known B").WithMultipleEpisodes().Build();
        dbContext.Set<RuvProgram>().AddRange(known1, known2);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(id: 1)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);
        _ruv.GetKidsTvAsync(Arg.Any<CancellationToken>()).Returns((RuvFeaturedTv?)null);

        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _syncQueue.Items.ShouldContain(x => x.RuvId == 90);
        _syncQueue.Items.ShouldContain(x => x.RuvId == 91);
    }
}
