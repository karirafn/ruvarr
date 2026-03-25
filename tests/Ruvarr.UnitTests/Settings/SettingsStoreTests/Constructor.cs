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
        store.Current.SonarrBaseAddress.ShouldBeEmpty();
        store.Current.SonarrApiKey.ShouldBeEmpty();
        store.Current.TvdbApiKey.ShouldBeEmpty();
        store.Current.TmdbApiKey.ShouldBeEmpty();
        store.Current.EpisodeDownloadDirectory.ShouldBe("tv");
        store.Current.MovieDownloadDirectory.ShouldBe("movies");
        store.Current.IgnoredChannels.ShouldBe(RuvarrSettings.Empty.IgnoredChannels);
        File.Exists(filePath).ShouldBeTrue();
    }

    [Fact]
    public void LoadsSettingsFromExistingFile()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(filePath, """
        {
            "SonarrBaseAddress": "http://localhost:8989",
            "SonarrApiKey": "test-key",
            "EpisodeDownloadDirectory": "episodes",
            "MovieDownloadDirectory": "movies"
        }
        """);

        // Act
        using SettingsStore store = new(filePath);

        // Assert
        store.Current.SonarrBaseAddress.ShouldBe("http://localhost:8989");
        store.Current.SonarrApiKey.ShouldBe("test-key");
        store.Current.EpisodeDownloadDirectory.ShouldBe("episodes");
        store.Current.MovieDownloadDirectory.ShouldBe("movies");
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
        store.Current.SonarrBaseAddress.ShouldBeEmpty();
        store.Current.SonarrApiKey.ShouldBeEmpty();
        store.Current.EpisodeDownloadDirectory.ShouldBe("tv");
        store.Current.MovieDownloadDirectory.ShouldBe("movies");
    }
}
