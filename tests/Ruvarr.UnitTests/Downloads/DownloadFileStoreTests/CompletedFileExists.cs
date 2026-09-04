using Ruvarr.Downloads;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.DownloadFileStoreTests;

public sealed class CompletedFileExists : IDisposable
{
    private readonly string _tempRoot;
    private readonly RuvarrSettings _settings;

    public CompletedFileExists()
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
    public void WhenFileExistsInCompletedDirectory_ReturnsTrue()
    {
        // Arrange
        string fileName = "Show.S01E01-RUV.mp4";
        Directory.CreateDirectory(_settings.ResolvedEpisodeDownloadDirectory);
        string completedPath = DownloadFileStore.CompletedPath(_settings, fileName);
        File.WriteAllText(completedPath, "episode data");

        // Act
        bool result = DownloadFileStore.CompletedFileExists(_settings, fileName);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenFileAbsentFromCompletedDirectory_ReturnsFalse()
    {
        // Arrange
        string fileName = "Show.S01E01-RUV.mp4";

        // Act
        bool result = DownloadFileStore.CompletedFileExists(_settings, fileName);

        // Assert
        result.ShouldBeFalse();
    }
}
