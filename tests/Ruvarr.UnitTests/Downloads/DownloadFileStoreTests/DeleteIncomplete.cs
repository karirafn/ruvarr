using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Downloads;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.DownloadFileStoreTests;

public sealed class DeleteIncomplete : IDisposable
{
    private readonly string _tempRoot;
    private readonly RuvarrSettings _settings;
    private readonly DownloadFileStore _sut = new(NullLogger<DownloadFileStore>.Instance);

    public DeleteIncomplete()
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
        Directory.CreateDirectory(_settings.ResolvedIncompleteDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void WhenFileExists_DeletesIncompleteFile()
    {
        // Arrange
        string fileName = "Show.S01E01-RUV.mp4";
        string filePath = DownloadFileStore.IncompletePath(_settings, fileName);
        File.WriteAllText(filePath, "data");

        // Act
        _sut.DeleteIncomplete(_settings, fileName);

        // Assert
        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public void WhenSiblingTmpFileExists_DeletesTmpSibling()
    {
        // Arrange
        string fileName = "Show.S01E01-RUV.mp4";
        string filePath = DownloadFileStore.IncompletePath(_settings, fileName);
        string siblingPath = Path.ChangeExtension(filePath, ".tmp" + Path.GetExtension(filePath));
        File.WriteAllText(filePath, "data");
        File.WriteAllText(siblingPath, "tmp data");

        // Act
        _sut.DeleteIncomplete(_settings, fileName);

        // Assert
        File.Exists(siblingPath).ShouldBeFalse();
    }

    [Fact]
    public void WhenFileDoesNotExist_IsNoOp()
    {
        // Arrange
        string fileName = "NonExistent.S01E01-RUV.mp4";

        // Act / Assert — no exception thrown
        Should.NotThrow(() => _sut.DeleteIncomplete(_settings, fileName));
    }
}
