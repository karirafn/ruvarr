using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Settings;
using Ruvarr.TvdbEpisodeLookup.Jobs;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbEpisodeLookupJobTests;

public sealed class SettingsGate
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ITvdbEpisodeMatcher _matcher = Substitute.For<ITvdbEpisodeMatcher>();
    private readonly TvdbEpisodeLookupNotifier _lookupQueue = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public SettingsGate()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _context.CancellationToken.Returns(CancellationToken.None);
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private TvdbEpisodeLookupJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<TvdbEpisodeLookupJob>.Instance,
        dbContext, _tvdb, _sonarr, _lookupQueue, new DomainEventBroadcaster(), _settingsStore, _matcher);

    [Fact]
    public async Task Skips_WhenTvdbIsNotConfigured()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            TvdbApiKey: ""));
        _lookupQueue.Enqueue(1, "Test Program");
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        _lookupQueue.Items.ShouldHaveSingleItem();
        _ = _tvdb.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        _ = _sonarr.DidNotReceive().GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_WhenSonarrIsNotConfigured()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        _settingsStore.Current.Returns(new RuvarrSettings(
            TvdbApiKey: "tvdb-key",
            SonarrBaseAddress: "", SonarrApiKey: ""));
        _lookupQueue.Enqueue(1, "Test Program");
        TvdbEpisodeLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        _lookupQueue.Items.ShouldHaveSingleItem();
        _ = _tvdb.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        _ = _sonarr.DidNotReceive().GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
