using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.RuvarrSettingsTests;

public sealed class ResolvedPaths
{
    [Fact]
    public void ResolvedEpisodeDownloadDirectory_CombinesRootAndSubdirectory()
    {
        // Arrange
        RuvarrSettings settings = new(EpisodeDownloadDirectory: "tv");

        // Act
        string result = settings.ResolvedEpisodeDownloadDirectory;

        // Assert
        result.ShouldBe(Path.Join("/downloads", "tv"));
    }

    [Fact]
    public void ResolvedMovieDownloadDirectory_CombinesRootAndSubdirectory()
    {
        // Arrange
        RuvarrSettings settings = new(MovieDownloadDirectory: "movies");

        // Act
        string result = settings.ResolvedMovieDownloadDirectory;

        // Assert
        result.ShouldBe(Path.Join("/downloads", "movies"));
    }

    [Fact]
    public void ResolvedEpisodeDownloadDirectory_HandlesCustomSubdirectory()
    {
        // Arrange
        RuvarrSettings settings = new(EpisodeDownloadDirectory: "series/episodes");

        // Act
        string result = settings.ResolvedEpisodeDownloadDirectory;

        // Assert
        result.ShouldBe(Path.Join("/downloads", "series/episodes"));
    }

    [Fact]
    public void ResolvedMovieDownloadDirectory_HandlesCustomSubdirectory()
    {
        // Arrange
        RuvarrSettings settings = new(MovieDownloadDirectory: "films");

        // Act
        string result = settings.ResolvedMovieDownloadDirectory;

        // Assert
        result.ShouldBe(Path.Join("/downloads", "films"));
    }

    [Fact]
    public void DownloadsRoot_IsCorrectConstant()
    {
        // Act & Assert
        RuvarrSettings.DownloadsRoot.ShouldBe("/downloads");
    }
}
