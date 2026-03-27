using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Jobs;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.DownloadQueueProcessorTests;

public sealed class SettingsGate
{
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly IFfmpegService _ffmpeg = Substitute.For<IFfmpegService>();
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

    private DownloadQueueProcessor CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<DownloadQueueProcessor>.Instance,
        dbContext, _sonarr, _ffmpeg, _settingsStore);

    private static async Task<DownloadQueueItem> SeedPendingItemAsync(RuvarrDbContext dbContext)
    {
        RuvProgram program = new RuvProgramBuilder().Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(program.Episodes[0]);
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item;
    }

    [Fact]
    public async Task Skips_WhenSonarrIsNotConfigured()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedPendingItemAsync(dbContext);
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "", SonarrApiKey: ""));
        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        item.Status.ShouldBe(DownloadQueueStatus.Pending);
        _ = _sonarr.DidNotReceive().GetManualImportsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
