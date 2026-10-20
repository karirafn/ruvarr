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

public sealed class SlugRefreshEnqueue
{
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ProgramRefreshNotifier _syncQueue = new(TimeProvider.System);
    private readonly TvdbSeriesLookupNotifier _tvdbLookupQueue = new();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public SlugRefreshEnqueue()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings { IgnoredChannels = [] });

        RuvFeaturedTv featured = new RuvFeaturedTvBuilder()
            .WithPrograms(new RuvTvProgramBuilder().WithId(1).Build())
            .Build();
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
        _settingsStore,
        _syncQueue,
        _tvdbLookupQueue,
        new DomainEventBroadcaster());

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
        await sut.Execute(_context, TestContext.Current.CancellationToken);

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
        await sut.Execute(_context, TestContext.Current.CancellationToken);

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
        await sut.Execute(_context, TestContext.Current.CancellationToken);

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

        RuvFeaturedTv featured = new RuvFeaturedTvBuilder()
            .WithPrograms(
                new RuvTvProgramBuilder().WithId(1).Build(),
                new RuvTvProgramBuilder().WithId(2).Build())
            .Build();
        _ruv.GetFeaturedTv(Arg.Any<CancellationToken>()).Returns(featured);

        RuvProgramRefreshJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        _tvdbLookupQueue.Items.Count.ShouldBe(1);
    }
}
