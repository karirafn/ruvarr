using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Settings;
using Ruvarr.TvdbSeriesLookup.Jobs;
using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbSeriesLookupJobTests;

public sealed class SettingsGate
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly TvdbSeriesLookupNotifier _lookupQueue = new();
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

    private TvdbSeriesLookupJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<TvdbSeriesLookupJob>.Instance,
        dbContext,
        _tvdb,
        _lookupQueue,
        new DomainEventBroadcaster(),
        _settingsStore);

    [Fact]
    public async Task Skips_WhenTvdbIsNotConfigured()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        _settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: ""));
        _lookupQueue.Enqueue(1, "Test Program");
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        _lookupQueue.Items.ShouldHaveSingleItem();
        _ = _tvdb.DidNotReceive().SearchAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }
}
