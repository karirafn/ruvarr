using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Infrastructure.Tmdb;
using Ruvarr.Jobs;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TmdbMovieLookupJobTests;

public sealed class SettingsGate
{
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

    [Fact]
    public async Task Skips_WhenTmdbIsNotConfigured()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        _settingsStore.Current.Returns(new RuvarrSettings(TmdbApiKey: ""));
        using TmdbClientProvider tmdbClientProvider = new(_settingsStore);
        TmdbMovieLookupJob sut = new(
            NullLogger<TmdbMovieLookupJob>.Instance,
            dbContext, tmdbClientProvider, _settingsStore);

        // Act
        await sut.Execute(_context);

        // Assert — should not query DB for programs (no exception from unconfigured TMDb client)
        dbContext.ChangeTracker.HasChanges().ShouldBeFalse();
    }
}
