using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Downloads;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.DownloadFileStoreTests;

public sealed class DeleteCompleted : IDisposable
{
    private readonly string _tempRoot;
    private readonly RuvarrSettings _settings;
    private readonly DownloadFileStore _sut = new(NullLogger<DownloadFileStore>.Instance);

    public DeleteCompleted()
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
        Directory.CreateDirectory(_settings.ResolvedEpisodeDownloadDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void WhenFileExists_DeletesCompletedFile()
    {
        // Arrange
        string fileName = "Show.S01E01-RUV.mp4";
        string filePath = DownloadFileStore.CompletedPath(_settings, fileName);
        File.WriteAllText(filePath, "data");

        // Act
        _sut.DeleteCompleted(_settings, fileName);

        // Assert
        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public void WhenOtherFileInCompletedDirectory_LeavesItInPlace()
    {
        // Arrange
        string otherPath = DownloadFileStore.CompletedPath(_settings, "Other.S01E02-RUV.mp4");
        File.WriteAllText(DownloadFileStore.CompletedPath(_settings, "Show.S01E01-RUV.mp4"), "data");
        File.WriteAllText(otherPath, "other");

        // Act
        _sut.DeleteCompleted(_settings, "Show.S01E01-RUV.mp4");

        // Assert
        File.Exists(otherPath).ShouldBeTrue();
    }

    [Fact]
    public void WhenFileDoesNotExist_IsNoOp()
    {
        // Arrange
        string fileName = "NonExistent.S01E01-RUV.mp4";

        // Act / Assert
        Should.NotThrow(() => _sut.DeleteCompleted(_settings, fileName));
    }
}
