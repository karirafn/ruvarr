using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.TvdbSeriesLookup.Jobs;
using Ruvarr.TvdbSeriesLookup.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbSeriesLookupJobTests;

public sealed class SlugRefresh
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly TvdbSeriesLookupNotifier _lookupQueue = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();

    public SlugRefresh()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _context.CancellationToken.Returns(CancellationToken.None);
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
        new DomainEventBroadcaster());

    [Fact]
    public async Task CallsGetSeriesAsync_WithParsedTvdbId_WhenSeriesIsNotNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).WithSlug(null).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        await _tvdb.Received(1).GetSeriesAsync(1000, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WritesSlugBack_AndSaves_WhenGetSeriesAsyncSucceeds()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).WithSlug(null).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        TvdbSeries? saved = await dbContext.Set<TvdbSeries>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        saved.ShouldNotBeNull();
        saved.Slug.ShouldBe("test-series");
    }

    [Fact]
    public async Task DoesNotCallSearchAsync_WhenSeriesIsNotNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).WithSlug(null).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        await _tvdb.DidNotReceive().SearchAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotCallScheduleLookup_WhenSeriesIsNotNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).WithSlug(null).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.NextLookup.ShouldBeNull();
    }

    [Fact]
    public async Task NoSideEffects_WhenGetSeriesAsyncReturnsNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).WithSlug(null).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns((SeriesData?)null);
        _lookupQueue.Enqueue(1, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        program.NextLookup.ShouldBeNull();
        program.Series!.Slug.ShouldBeNull();
    }
}
