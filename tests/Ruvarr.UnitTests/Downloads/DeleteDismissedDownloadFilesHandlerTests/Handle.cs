using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Downloads;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Events;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.DeleteDismissedDownloadFilesHandlerTests;

public sealed class Handle : IDisposable
{
    private readonly string _tempRoot;
    private readonly RuvarrSettings _settings;
    private readonly DeleteDismissedDownloadFilesHandler _sut;

    public Handle()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ruvarr-dismiss-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRoot);
        _settings = new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr",
            SonarrApiKey: "key",
            EpisodeDownloadDirectory: "episodes")
        {
            DownloadsRoot = _tempRoot
        };
        Directory.CreateDirectory(_settings.ResolvedEpisodeDownloadDirectory);
        Directory.CreateDirectory(_settings.ResolvedIncompleteDirectory);

        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(_settings);

        _sut = new DeleteDismissedDownloadFilesHandler(
            new DownloadFileStore(NullLogger<DownloadFileStore>.Instance),
            settingsStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static DownloadDismissedEvent DismissedFailedItem(out string fileName)
    {
        DownloadQueueItem item = new DownloadQueueItemBuilder().Failed("reason").Build();
        item.Dismiss();
        fileName = item.FileName!;
        return new DownloadDismissedEvent(item);
    }

    private static string TmpSibling(string path) =>
        Path.ChangeExtension(path, ".tmp" + Path.GetExtension(path));

    [Fact]
    public async Task DeletesCompletedFile()
    {
        // Arrange
        DownloadDismissedEvent @event = DismissedFailedItem(out string fileName);
        string completedPath = DownloadFileStore.CompletedPath(_settings, fileName);
        await File.WriteAllTextAsync(completedPath, "data", TestContext.Current.CancellationToken);

        // Act
        await _sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(completedPath).ShouldBeFalse();
    }

    [Fact]
    public async Task DeletesIncompleteFile()
    {
        // Arrange
        DownloadDismissedEvent @event = DismissedFailedItem(out string fileName);
        string incompletePath = DownloadFileStore.IncompletePath(_settings, fileName);
        await File.WriteAllTextAsync(incompletePath, "data", TestContext.Current.CancellationToken);

        // Act
        await _sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(incompletePath).ShouldBeFalse();
    }

    [Fact]
    public async Task DeletesIncompleteTmpSibling()
    {
        // Arrange
        DownloadDismissedEvent @event = DismissedFailedItem(out string fileName);
        string siblingPath = TmpSibling(DownloadFileStore.IncompletePath(_settings, fileName));
        await File.WriteAllTextAsync(siblingPath, "data", TestContext.Current.CancellationToken);

        // Act
        await _sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(siblingPath).ShouldBeFalse();
    }

    [Fact]
    public async Task WhenNoFilesExist_CompletesWithoutError()
    {
        // Arrange
        DownloadDismissedEvent @event = DismissedFailedItem(out _);

        // Act / Assert
        await Should.NotThrowAsync(() => _sut.Handle(@event, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenFileNameIsNull_TouchesNoFiles()
    {
        // Arrange
        DownloadQueueItem item = new DownloadQueueItemBuilder().Build();
        string episodeFileName = item.Episode.ToFilename();
        string completedPath = DownloadFileStore.CompletedPath(_settings, episodeFileName);
        string incompletePath = DownloadFileStore.IncompletePath(_settings, episodeFileName);
        await File.WriteAllTextAsync(completedPath, "data", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(incompletePath, "data", TestContext.Current.CancellationToken);

        // Act
        await _sut.Handle(new DownloadDismissedEvent(item), TestContext.Current.CancellationToken);

        // Assert
        item.FileName.ShouldBeNull();
        File.Exists(completedPath).ShouldBeTrue();
        File.Exists(incompletePath).ShouldBeTrue();
    }
}
