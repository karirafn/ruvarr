using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;
using Ruvarr.TvdbEpisodeLookup.Jobs;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbEpisodeLookupJobTests;

public sealed class ExceptionHandling
{
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ITvdbEpisodeMatcher _matcher = Substitute.For<ITvdbEpisodeMatcher>();
    private readonly TvdbEpisodeLookupNotifier _notifier = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public ExceptionHandling()
    {
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            TvdbApiKey: "tvdb-key", TmdbApiKey: "tmdb-key"));
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private TvdbEpisodeLookupJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<TvdbEpisodeLookupJob>.Instance,
        dbContext, _tvdb, _sonarr, _notifier, new DomainEventBroadcaster(), _settingsStore, _matcher);

    [Fact]
    public async Task MarksComplete_WhenTvdbClientThrows()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(1000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("TVDB API unavailable"));
        _notifier.Enqueue(1, program.Name);
        _notifier.Items.ShouldHaveSingleItem();
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        _notifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task MarksComplete_WhenSonarrClientThrows()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(2000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(2).Build();
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Episode 2", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tvdb.GetSeriesAsync(2000, Arg.Any<CancellationToken>())
            .Returns(new TvdbSeriesDataBuilder().WithId(2000).Build());
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Sonarr unavailable"));
        _notifier.Enqueue(2, program.Name);
        _notifier.Items.ShouldHaveSingleItem();
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        _notifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenHttpClientTimesOut_MarksComplete()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(4000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(4).Build();
        program.TryAddEpisode("ep0004", new Uri("http://test.com"), "Episode 4", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tvdb.GetSeriesAsync(4000, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("HttpClient timeout"));
        _notifier.Enqueue(4, program.Name);
        _notifier.Items.ShouldHaveSingleItem();
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, cts.Token);

        // Assert
        _notifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenCancelled_PropagatesOperationCanceledException()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(3000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(3).Build();
        program.TryAddEpisode("ep0003", new Uri("http://test.com"), "Episode 3", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tvdb.GetSeriesAsync(3000, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        _notifier.Enqueue(3, program.Name);
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);
        await cts.CancelAsync();

        // Act
        Func<Task> act = async () => await sut.Execute(_context, cts.Token);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
