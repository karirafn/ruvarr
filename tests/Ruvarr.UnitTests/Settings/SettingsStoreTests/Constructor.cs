using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.SettingsStoreTests;

public sealed class Constructor : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public Constructor()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void ReturnsEmptySettingsWhenFileDoesNotExist()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");

        // Act
        using SettingsStore store = new(filePath);

        // Assert
        store.Current.ShouldBe(RuvarrSettings.Empty);
    }

    [Fact]
    public void LoadsSettingsFromExistingFile()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(filePath, """
        {
            "SonarrBaseUrl": "http://localhost:8989",
            "SonarrApiKey": "test-key",
            "EpisodeDownloadDirectory": "/downloads/episodes",
            "MovieDownloadDirectory": "/downloads/movies"
        }
        """);

        // Act
        using SettingsStore store = new(filePath);

        // Assert
        store.Current.SonarrBaseUrl.ShouldBe("http://localhost:8989");
        store.Current.SonarrApiKey.ShouldBe("test-key");
        store.Current.EpisodeDownloadDirectory.ShouldBe("/downloads/episodes");
        store.Current.MovieDownloadDirectory.ShouldBe("/downloads/movies");
    }

    [Fact]
    public void ReturnsEmptySettingsWhenFileContainsEmptyJson()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(filePath, "{}");

        // Act
        using SettingsStore store = new(filePath);

        // Assert
        store.Current.SonarrBaseUrl.ShouldBeNull();
        store.Current.SonarrApiKey.ShouldBeNull();
        store.Current.EpisodeDownloadDirectory.ShouldBeNull();
        store.Current.MovieDownloadDirectory.ShouldBeNull();
    }
}
