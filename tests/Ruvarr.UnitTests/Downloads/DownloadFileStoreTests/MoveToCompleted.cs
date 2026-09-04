using Ruvarr.Downloads;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.DownloadFileStoreTests;

public sealed class MoveToCompleted : IDisposable
{
    private readonly string _tempRoot;
    private readonly RuvarrSettings _settings;

    public MoveToCompleted()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ruvarr-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRoot);
        _settings = new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr",
            SonarrApiKey: "key",
            EpisodeDownloadDirectory: "episodes")
        {
            DownloadsRoot = _tempRoot
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void WhenIncompleteFileExists_MovesFileToCompletedDirectory_AndReturnsCompletedPath()
    {
        // Arrange
        string fileName = "Show.S01E01-RUV.mp4";
        Directory.CreateDirectory(_settings.ResolvedIncompleteDirectory);
        string incompletePath = DownloadFileStore.IncompletePath(_settings, fileName);
        File.WriteAllText(incompletePath, "episode data");

        // Act
        string result = DownloadFileStore.MoveToCompleted(_settings, fileName);

        // Assert
        string expectedCompletedPath = DownloadFileStore.CompletedPath(_settings, fileName);
        result.ShouldBe(expectedCompletedPath);
        File.Exists(expectedCompletedPath).ShouldBeTrue();
        File.Exists(incompletePath).ShouldBeFalse();
    }

    [Fact]
    public void WhenDestinationAlreadyExists_OverwritesExistingFile()
    {
        // Arrange
        string fileName = "Show.S01E01-RUV.mp4";
        Directory.CreateDirectory(_settings.ResolvedIncompleteDirectory);
        Directory.CreateDirectory(_settings.ResolvedEpisodeDownloadDirectory);

        string incompletePath = DownloadFileStore.IncompletePath(_settings, fileName);
        string completedPath = DownloadFileStore.CompletedPath(_settings, fileName);

        File.WriteAllText(incompletePath, "new episode data");
        File.WriteAllText(completedPath, "old episode data");

        // Act
        DownloadFileStore.MoveToCompleted(_settings, fileName);

        // Assert
        File.ReadAllText(completedPath).ShouldBe("new episode data");
    }
}
