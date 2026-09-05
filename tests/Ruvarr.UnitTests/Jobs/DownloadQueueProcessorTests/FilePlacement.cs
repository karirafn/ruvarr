using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Notifiers;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Jobs;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.DownloadQueueProcessorTests;

public sealed class FilePlacement : IDisposable
{
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly IFfmpegService _ffmpeg = Substitute.For<IFfmpegService>();
    private readonly IRuvStreamInspector _streamInspector = Substitute.For<IRuvStreamInspector>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly DownloadProgressNotifier _progressNotifier;
    private readonly DownloadFileStore _fileStore;
    private readonly string _tempRoot;
    private readonly RuvarrSettings _settings;

    public FilePlacement()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ruvarr-fp-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRoot);

        _settings = new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr",
            SonarrApiKey: "key",
            EpisodeDownloadDirectory: "episodes")
        {
            DownloadsRoot = _tempRoot
        };

        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(_settings);

        _progressNotifier = new DownloadProgressNotifier(
            Substitute.For<IDomainEventBroadcaster>(), TimeProvider.System);

        _fileStore = new DownloadFileStore(NullLogger<DownloadFileStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private DownloadQueueProcessor CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<DownloadQueueProcessor>.Instance,
        dbContext, _ffmpeg, _streamInspector, _settingsStore, _progressNotifier, _fileStore,
        new SonarrImporter(_sonarr, dbContext, NullLogger<SonarrImporter>.Instance));

    private static async Task<DownloadQueueItem> SeedItemAsync(RuvarrDbContext dbContext)
    {
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com/stream"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(program.Episodes[0]);
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item;
    }

    [Fact]
    public async Task WhenDownloading_FfmpegTargetsIncompletePath_NothingInCompleted()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedItemAsync(dbContext);

        string? capturedDownloadTarget = null;
        _ffmpeg
            .DownloadAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<FfmpegProgressData>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedDownloadTarget = callInfo.ArgAt<string>(1);
                return Task.CompletedTask;
            });

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        string expectedIncomplete = _settings.ResolvedIncompleteDirectory;
        capturedDownloadTarget.ShouldNotBeNull();
        capturedDownloadTarget.ShouldStartWith(expectedIncomplete);

        string completedDir = _settings.ResolvedEpisodeDownloadDirectory;
        bool anyCompletedFiles = Directory.Exists(completedDir) && Directory.GetFiles(completedDir).Length > 0;
        anyCompletedFiles.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenTrimming_TrimRunsAgainstIncompletePath_NoTmpMp4InCompleted()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedItemAsync(dbContext);

        string? capturedTrimTarget = null;

        _ffmpeg
            .DownloadAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<FfmpegProgressData>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                string targetPath = callInfo.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await File.WriteAllTextAsync(targetPath, "fake-video", CancellationToken.None);
            });

        _ffmpeg
            .DetectTrimPointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedTrimTarget = callInfo.ArgAt<string>(0);
                return Task.FromResult<TimeSpan?>(TimeSpan.FromSeconds(5));
            });

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        string expectedIncomplete = _settings.ResolvedIncompleteDirectory;
        capturedTrimTarget.ShouldNotBeNull();
        capturedTrimTarget.ShouldStartWith(expectedIncomplete);

        string completedDir = _settings.ResolvedEpisodeDownloadDirectory;
        bool anyTmpInCompleted = Directory.Exists(completedDir)
            && Directory.GetFiles(completedDir, "*.tmp.mp4").Length > 0;
        anyTmpInCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenDownloadSucceeds_FileMovedFlatToCompleted_NoSeriesSubdir()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedItemAsync(dbContext);

        _ffmpeg
            .DownloadAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<FfmpegProgressData>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                string targetPath = callInfo.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await File.WriteAllTextAsync(targetPath, "fake-video", CancellationToken.None);
            });

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert: file is in completed dir directly (no subdirectory)
        string completedDir = _settings.ResolvedEpisodeDownloadDirectory;
        string[] completedFiles = Directory.GetFiles(completedDir);
        completedFiles.Length.ShouldBe(1);

        string completedFile = completedFiles[0];
        Path.GetDirectoryName(completedFile).ShouldBe(completedDir);

        item.FileName.ShouldNotBeNull();
        Path.GetFileName(completedFile).ShouldBe(item.FileName);
    }

    [Fact]
    public async Task WhenFileAlreadyExistsInCompleted_MoveOverwritesExisting()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        await SeedItemAsync(dbContext);

        _ffmpeg
            .DownloadAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<FfmpegProgressData>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                string targetPath = callInfo.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await File.WriteAllTextAsync(targetPath, "new-content", CancellationToken.None);
            });

        string completedDir = _settings.ResolvedEpisodeDownloadDirectory;
        Directory.CreateDirectory(completedDir);

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert: exactly one file in completed (was moved, not duplicated)
        string[] completedFiles = Directory.GetFiles(completedDir);
        completedFiles.Length.ShouldBe(1);
        string content = await File.ReadAllTextAsync(completedFiles[0], TestContext.Current.CancellationToken);
        content.ShouldBe("new-content");
    }

    [Fact]
    public async Task WhenFfmpegDownloadFails_DeletesIncompleteFile_AndMarksFailed()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedItemAsync(dbContext);

        _ffmpeg
            .DownloadAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<FfmpegProgressData>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                // Write a partial file to simulate partial download, then fail
                string targetPath = callInfo.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await File.WriteAllTextAsync(targetPath, "partial", CancellationToken.None);
                throw new InvalidOperationException("ffmpeg crashed");
            });

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        item.Status.ShouldBe(DownloadQueueStatus.Failed);
        item.FailureReason.ShouldBe("FFmpeg download failed");

        string incompleteDir = _settings.ResolvedIncompleteDirectory;
        bool partialFileExists = Directory.Exists(incompleteDir)
            && Directory.GetFiles(incompleteDir).Length > 0;
        partialFileExists.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenMoveToCompletedFails_LeavesFileOnDisk_AndMarksFailed()
    {
        // Arrange — download to incomplete succeeds; we block the completed directory to force move failure
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedItemAsync(dbContext);

        _ffmpeg
            .DownloadAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IProgress<FfmpegProgressData>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                string targetPath = callInfo.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await File.WriteAllTextAsync(targetPath, "video-data", CancellationToken.None);
            });

        // Block the completed directory by writing a file at its path so Directory.CreateDirectory fails
        string completedDir = _settings.ResolvedEpisodeDownloadDirectory;
        string completedParentDir = Path.GetDirectoryName(completedDir)!;
        Directory.CreateDirectory(completedParentDir);
        await File.WriteAllTextAsync(completedDir, "blocker", TestContext.Current.CancellationToken);

        DownloadQueueProcessor sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert
        item.Status.ShouldBe(DownloadQueueStatus.Failed);
        item.FailureReason.ShouldBe("Failed to move file to completed directory");

        // Incomplete file must still be on disk (not deleted on move failure)
        string incompleteDir = _settings.ResolvedIncompleteDirectory;
        bool incompleteFileExists = Directory.Exists(incompleteDir)
            && Directory.GetFiles(incompleteDir).Length > 0;
        incompleteFileExists.ShouldBeTrue();
    }
}
