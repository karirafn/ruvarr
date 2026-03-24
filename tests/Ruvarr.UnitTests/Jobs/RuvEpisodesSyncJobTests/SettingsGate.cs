using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Jobs;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvEpisodesSyncJobTests;

public sealed class SettingsGate
{
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
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

    private RuvEpisodesSyncJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<RuvEpisodesSyncJob>.Instance,
        _ruv, dbContext, _sonarr, _syncQueue, new DomainEventBroadcaster(), _settingsStore);

    [Fact]
    public async Task Skips_WhenSonarrIsNotConfigured()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        _settingsStore.Current.Returns(new RuvarrSettings(SonarrBaseAddress: "", SonarrApiKey: ""));
        _syncQueue.Enqueue(1, "Test Program");
        RuvEpisodesSyncJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        _syncQueue.Items.ShouldHaveSingleItem();
        _ = _sonarr.DidNotReceive().GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
