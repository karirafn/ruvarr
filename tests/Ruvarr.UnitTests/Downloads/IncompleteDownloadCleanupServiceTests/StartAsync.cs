using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.IncompleteDownloadCleanupServiceTests;

// A Downloading item at startup can only mean a crashed prior process —
// [DisallowConcurrentExecution] makes a live Downloading item at boot
// structurally impossible on a healthy single-instance app.
public sealed class StartAsync : IDisposable
{
    private readonly string _tempRoot;
    private readonly RuvarrSettings _settings;
    private readonly DownloadFileStore _fileStore;
    private readonly ServiceProvider _serviceProvider;

    public StartAsync()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ruvarr-cleanup-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRoot);

        _settings = new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr",
            SonarrApiKey: "key",
            EpisodeDownloadDirectory: "episodes")
        {
            DownloadsRoot = _tempRoot
        };

        _fileStore = new DownloadFileStore(NullLogger<DownloadFileStore>.Instance);

        string databaseName = Guid.NewGuid().ToString();
        ServiceCollection services = new();
        services.AddDbContext<RuvarrDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        _serviceProvider = services.BuildServiceProvider();

        Directory.CreateDirectory(_settings.ResolvedIncompleteDirectory);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private async Task<DownloadQueueItem> SeedDownloadingItemAsync()
    {
        await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = new RuvProgramBuilder().Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com/stream"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(program.Episodes[0]);
        item.MarkDownloading();
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item;
    }

    private IncompleteDownloadCleanupService CreateSut()
    {
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(_settings);

        return new IncompleteDownloadCleanupService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _fileStore,
            settingsStore,
            NullLogger<IncompleteDownloadCleanupService>.Instance);
    }

    [Fact]
    public async Task WhenDownloadingItemHasFileName_DeletesIncompleteFile()
    {
        // Arrange
        DownloadQueueItem item = await SeedDownloadingItemAsync();
        item.FileName.ShouldNotBeNull();

        string incompleteFilePath = DownloadFileStore.IncompletePath(_settings, item.FileName);
        await File.WriteAllTextAsync(incompleteFilePath, "partial", TestContext.Current.CancellationToken);

        IncompleteDownloadCleanupService sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        File.Exists(incompleteFilePath).ShouldBeFalse();
    }

    [Fact]
    public async Task WhenDownloadingItemHasNullFileName_SkipsItemWithoutThrowing()
    {
        // Arrange — emulate a pre-migration row: a Downloading item whose FileName was never set.
        // MarkDownloading() always sets FileName, so we bypass the domain transition and set Status
        // directly via EF entry to reach this otherwise-unreachable state (deserialization / legacy DB row).
        await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = new RuvProgramBuilder().Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com/stream"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(program.Episodes[0]);
        dbContext.Set<DownloadQueueItem>().Add(item);

        // Bypass domain transition to set Status = Downloading while FileName stays null
        dbContext.Entry(item).Property("Status").CurrentValue = DownloadQueueStatus.Downloading;
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        item.FileName.ShouldBeNull();

        IncompleteDownloadCleanupService sut = CreateSut();

        // Act / Assert — no exception thrown; null FileName must not be used to compose a path
        await Should.NotThrowAsync(() => sut.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenDownloadingItemHasFileName_DeletesTmpMp4Sibling()
    {
        // Arrange
        DownloadQueueItem item = await SeedDownloadingItemAsync();
        item.FileName.ShouldNotBeNull();

        string incompleteFilePath = DownloadFileStore.IncompletePath(_settings, item.FileName);
        string siblingPath = Path.ChangeExtension(incompleteFilePath, ".tmp" + Path.GetExtension(incompleteFilePath));
        await File.WriteAllTextAsync(incompleteFilePath, "partial", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(siblingPath, "tmp-data", TestContext.Current.CancellationToken);

        IncompleteDownloadCleanupService sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        File.Exists(siblingPath).ShouldBeFalse();
    }

    [Fact]
    public async Task WhenDownloadingItemExists_StatusIsUnchangedAfterSweep()
    {
        // Arrange — cleanup only deletes files; it must never mutate queue item status
        DownloadQueueItem item = await SeedDownloadingItemAsync();
        item.FileName.ShouldNotBeNull();

        string incompleteFilePath = DownloadFileStore.IncompletePath(_settings, item.FileName);
        await File.WriteAllTextAsync(incompleteFilePath, "partial", TestContext.Current.CancellationToken);

        IncompleteDownloadCleanupService sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Assert — status stays Downloading; the queue processor handles cleanup transitions
        item.Status.ShouldBe(DownloadQueueStatus.Downloading);
    }

    [Fact]
    public async Task WhenNonDownloadingItemExists_IsNotTouched()
    {
        // Arrange — items in Pending/Complete/Failed are not crash orphans and must be skipped
        await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = new RuvProgramBuilder().Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com/stream"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem pendingItem = DownloadQueueItem.Create(program.Episodes[0]);
        dbContext.Set<DownloadQueueItem>().Add(pendingItem);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IncompleteDownloadCleanupService sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Assert — Pending item is untouched (Status, FileName unchanged)
        pendingItem.Status.ShouldBe(DownloadQueueStatus.Pending);
        pendingItem.FileName.ShouldBeNull();
    }
}
