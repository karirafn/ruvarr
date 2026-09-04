using Ruvarr.Downloads;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.DownloadFileStoreTests;

public sealed class PathDerivation : IDisposable
{
    private readonly string _tempRoot;
    private readonly RuvarrSettings _settings;

    public PathDerivation()
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
    public void IncompletePath_ReturnsFileUnderIncompleteDirectory()
    {
        // Arrange
        string fileName = "Show.S01E01-RUV.mp4";

        // Act
        string result = DownloadFileStore.IncompletePath(_settings, fileName);

        // Assert
        result.ShouldBe(Path.Join(_settings.ResolvedIncompleteDirectory, fileName));
    }

    [Fact]
    public void CompletedPath_IsFlat_ReturnsFileUnderEpisodeDownloadDirectory()
    {
        // Arrange
        string fileName = "Show.S01E01-RUV.mp4";

        // Act
        string result = DownloadFileStore.CompletedPath(_settings, fileName);

        // Assert
        result.ShouldBe(Path.Join(_settings.ResolvedEpisodeDownloadDirectory, fileName));
    }
}
