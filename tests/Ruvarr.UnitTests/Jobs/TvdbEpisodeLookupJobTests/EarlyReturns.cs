using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.TvdbEpisodeLookup.Jobs;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbEpisodeLookupJobTests;

public sealed class EarlyReturns
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly TvdbEpisodeLookupNotifier _notifier = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public EarlyReturns()
    {
        _sonarr.GetMissingEpisodesAsync().Returns([]);
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private TvdbEpisodeLookupJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<TvdbEpisodeLookupJob>.Instance,
        dbContext, _tvdb, _sonarr, _notifier, new DomainEventBroadcaster());

    [Fact]
    public async Task ReturnsWhenQueueIsEmpty()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _ = _tvdb.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsWhenProgramNotFound()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        _notifier.Enqueue(1, "Program A");
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _ = _tvdb.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsWhenProgramHasNoSeries()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _ = _tvdb.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        program.Episodes[0].TvdbId.ShouldBeNull();
    }

    [Fact]
    public async Task SchedulesLookupWhenSeriesDataNotFound()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns((SeriesData?)null);
        _notifier.Enqueue(1, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(null!);

        // Assert
        program.Episodes[0].NextLookup.ShouldNotBeNull();
    }
}
