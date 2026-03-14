using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.Jobs;
using Ruvarr.Programs;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvProgramRefreshJobTests;

public sealed class SlugRefreshEnqueue
{
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
    private readonly TvdbSeriesLookupNotifier _tvdbLookupQueue = new();
    private readonly IOptions<RuvarrOptions> _options = Options.Create(new RuvarrOptions
    {
        DownloadsRootDirectory = "/tmp",
        EpisodeDownloadDirectory = "episodes",
        MovieDownloadDirectory = "movies",
        IgnoredChannels = []
    });

    public SlugRefreshEnqueue()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow, [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style", [CreateRuvTvProgram(id: 1)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);
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
        _options,
        _syncQueue,
        _tvdbLookupQueue);

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
    public async Task EnqueuesProgram_WhenSeriesNotNullAndSlugIsNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).WithSlug(null).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _tvdbLookupQueue.Items.ShouldHaveSingleItem();
        _tvdbLookupQueue.Items[0].RuvId.ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotEnqueue_WhenSeriesNotNullAndSlugIsNotNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).WithSlug("some-slug").Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _tvdbLookupQueue.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnqueuesViaUnmatchedPath_WhenSeriesIsNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert - unmatched program goes via the unmatched path (only one enqueue total)
        _tvdbLookupQueue.Items.ShouldHaveSingleItem();
        _tvdbLookupQueue.Items[0].RuvId.ShouldBe(1);
    }

    [Fact]
    public async Task DeduplicatesByTvdbId_WhenTwoProgramsShareSameSeries()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).WithSlug(null).Build();

        RuvProgram program1 = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        program1.MatchTvdb(series);

        RuvProgram program2 = new RuvProgramBuilder().WithRuvId(2).WithMultipleEpisodes().Build();
        program2.MatchTvdb(series);

        dbContext.Set<RuvProgram>().AddRange(program1, program2);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        RuvFeaturedTv featured = new(DateTimeOffset.UtcNow,
            [new RuvPanel(DateTimeOffset.UtcNow, "Panel", "panel", "type", "style",
                [CreateRuvTvProgram(id: 1), CreateRuvTvProgram(id: 2)])]);
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _tvdbLookupQueue.Items.Count.ShouldBe(1);
    }
}
