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
using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvProgramRefreshJobTests;

public sealed class IgnoredProgramsFilter
{
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
    private readonly TvdbSeriesLookupNotifier _tvdbLookupQueue = new();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public IgnoredProgramsFilter()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
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

    private static RuvTvProgram CreateRuvTvProgram(int id, string title, string channel = "ruv") => new(
        LastUpdated: DateTimeOffset.UtcNow,
        Id: id,
        Title: title,
        ForeignTitle: $"Foreign {title}",
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
        MultipleEpisodes: true,
        Episodes: [],
        ReverseEpisodeOrder: false,
        WebAvailableEpisodes: 1,
        VodAvailableEpisodes: 0,
        PodcastVailableEpisodes: 0,
        WebLatestDate: DateTime.UtcNow,
        Channel: channel,
        WebPlayerUrl: new Uri("http://example.com/player"));

    [Fact]
    public async Task ExcludesProgramsWhoseTitleIsInIgnoredPrograms()
    {
        // Arrange
        _settingsStore.Current.Returns(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = ["Fréttir"] });

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(1, "Fréttir"), CreateRuvTvProgram(2, "Kastljós")])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        List<RuvProgram> programs = await dbContext.Set<RuvProgram>().ToListAsync(TestContext.Current.CancellationToken);
        programs.ShouldHaveSingleItem();
        programs[0].Name.ShouldBe("Kastljós");
    }

    [Fact]
    public async Task ExcludesProgramsCaseInsensitively()
    {
        // Arrange
        _settingsStore.Current.Returns(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = ["fréttir"] });

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(1, "Fréttir"), CreateRuvTvProgram(2, "Kastljós")])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        List<RuvProgram> programs = await dbContext.Set<RuvProgram>().ToListAsync(TestContext.Current.CancellationToken);
        programs.ShouldHaveSingleItem();
        programs[0].Name.ShouldBe("Kastljós");
    }

    [Fact]
    public async Task IncludesAllProgramsWhenIgnoredProgramsIsEmpty()
    {
        // Arrange
        _settingsStore.Current.Returns(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = [] });

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(1, "Fréttir"), CreateRuvTvProgram(2, "Kastljós")])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        List<RuvProgram> programs = await dbContext.Set<RuvProgram>().ToListAsync(TestContext.Current.CancellationToken);
        programs.Count.ShouldBe(2);
    }
}
