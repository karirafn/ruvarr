using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Settings;
using Ruvarr.SonarrFolderCheck;
using Ruvarr.Testing.Logging;
using Ruvarr.Testing.Time;

using Shouldly;

namespace Ruvarr.UnitTests.SonarrFolderCheck.SonarrFolderCheckerTests;

public sealed class CheckAsync : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly SonarrFolderCheckHealth _health = new();
    private readonly IDomainEventBroadcaster _broadcaster = Substitute.For<IDomainEventBroadcaster>();
    private readonly RecordingLogger<SonarrFolderChecker> _logger = new();
    private readonly string _downloadsRoot;
    private readonly RuvarrSettings _settings;

    public CheckAsync()
    {
        _downloadsRoot = Path.Combine(Path.GetTempPath(), $"ruvarr-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_downloadsRoot);
        _settings = new RuvarrSettings(SonarrBaseAddress: "http://sonarr:8989/", SonarrApiKey: "key", EpisodeDownloadDirectory: "tv")
        {
            DownloadsRoot = _downloadsRoot,
        };
        _settingsStore.Current.Returns(_settings);
    }

    private string Folder => _settings.ResolvedEpisodeDownloadDirectory;

    private string ProbePath => Path.Join(Folder, SonarrFolderChecker.ProbeFileName);

    public void Dispose()
    {
        if (Directory.Exists(_downloadsRoot))
        {
            Directory.Delete(_downloadsRoot, recursive: true);
        }
    }

    private SonarrFolderChecker CreateSut() =>
        new(_settingsStore, _sonarr, _health, _broadcaster, new FixedTimeProvider(Now), _logger);

    private void GivenScanLists(params string[] paths) =>
        _sonarr.GetManualImportsAsync(Folder, null, Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<ManualImportFile>>(paths.Select(ImportFile).ToList()));

    private static ManualImportFile ImportFile(string path) => new(
        Path: path,
        RelativePath: Path.GetFileName(path),
        Name: Path.GetFileName(path),
        Size: 0,
        Series: null,
        SeasonNumber: 0,
        Episodes: [],
        Quality: new QualityContainer(new Quality(0, "Unknown", "unknown", 0), new Revision(1, 0, false)),
        Languages: [],
        QualityWeight: 0,
        CustomFormatScore: 0,
        IndexerFlags: 0,
        ReleaseType: "unknown",
        Id: 1);

    [Fact]
    public async Task RecordsPassing_WhenScanListsTheProbeFile()
    {
        // Arrange
        GivenScanLists("/downloads/tv/Other.S01E01.mkv", $"/downloads/tv/{SonarrFolderChecker.ProbeFileName}");

        // Act
        SonarrFolderCheckResult result = await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(new SonarrFolderCheckResult(SonarrFolderCheckStatus.Passing, Folder, Now));
        _health.LastResult.ShouldBe(result);
    }

    [Fact]
    public async Task RecordsFailing_WhenScanSucceedsWithoutTheProbeFile()
    {
        // Arrange
        GivenScanLists("/downloads/tv/Other.S01E01.mkv");

        // Act
        SonarrFolderCheckResult result = await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(new SonarrFolderCheckResult(SonarrFolderCheckStatus.Failing, Folder, Now));
        _health.LastResult.ShouldBe(result);
    }

    [Fact]
    public async Task RecordsUnknown_WhenScanFails()
    {
        // Arrange
        _sonarr.GetManualImportsAsync(Folder, null, Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<ManualImportFile>>(ApiClientErrors.RequestFailed));

        // Act
        SonarrFolderCheckResult result = await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(new SonarrFolderCheckResult(SonarrFolderCheckStatus.Unknown, Folder, Now));
    }

    [Fact]
    public async Task RecordsUnknownWithoutScanning_WhenSonarrIsNotConfigured()
    {
        // Arrange
        _settingsStore.Current.Returns(_settings with { SonarrApiKey = "" });

        // Act
        SonarrFolderCheckResult result = await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(SonarrFolderCheckStatus.Unknown);
        await _sonarr.DidNotReceive().GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
        File.Exists(ProbePath).ShouldBeFalse();
    }

    [Fact]
    public async Task RecordsUnknownAndLogsError_WhenProbeFileCannotBeWritten()
    {
        // Arrange: a file where the downloads root's subfolder should be makes the folder uncreatable.
        await File.WriteAllTextAsync(Folder, "not a directory", TestContext.Current.CancellationToken);

        // Act
        SonarrFolderCheckResult result = await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(SonarrFolderCheckStatus.Unknown);
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Error);
        await _sonarr.DidNotReceive().GetManualImportsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WritesEmptyProbeFileBeforeScanning()
    {
        // Arrange
        long? sizeDuringScan = null;
        _sonarr.GetManualImportsAsync(Folder, null, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                sizeDuringScan = File.Exists(ProbePath) ? new FileInfo(ProbePath).Length : null;
                return new Result<IReadOnlyList<ManualImportFile>>(Array.Empty<ManualImportFile>());
            });

        // Act
        await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        sizeDuringScan.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeletesProbeFileAfterScan(bool listed)
    {
        // Arrange
        if (listed)
        {
            GivenScanLists(ProbePath);
        }
        else
        {
            GivenScanLists();
        }

        // Act
        await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        File.Exists(ProbePath).ShouldBeFalse();
    }

    [Fact]
    public async Task DeletesProbeFile_WhenScanThrows()
    {
        // Arrange
        _sonarr.GetManualImportsAsync(Folder, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => CreateSut().CheckAsync(TestContext.Current.CancellationToken));

        // Assert
        File.Exists(ProbePath).ShouldBeFalse();
    }

    [Fact]
    public async Task OverwritesProbeFileLeftBehindByAnEarlierCheck()
    {
        // Arrange
        Directory.CreateDirectory(Folder);
        await File.WriteAllTextAsync(ProbePath, "left behind", TestContext.Current.CancellationToken);
        GivenScanLists(ProbePath);

        // Act
        SonarrFolderCheckResult result = await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(SonarrFolderCheckStatus.Passing);
        File.Exists(ProbePath).ShouldBeFalse();
    }

    [Fact]
    public async Task PublishesSonarrFolderCheckedEvent()
    {
        // Arrange
        GivenScanLists();

        // Act
        await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        _broadcaster.Received(1).Publish(Arg.Any<SonarrFolderCheckedEvent>());
    }
}
