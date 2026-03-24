using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.RuvarrSettingsTests;

public sealed class ReadinessProperties
{
    [Fact]
    public void IsTvdbConfigured_ReturnsFalse_WhenTvdbApiKeyIsEmpty()
    {
        // Arrange
        RuvarrSettings settings = new(TvdbApiKey: "");

        // Act
        bool result = settings.IsTvdbConfigured;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsTvdbConfigured_ReturnsFalse_WhenTvdbApiKeyIsWhitespace()
    {
        // Arrange
        RuvarrSettings settings = new(TvdbApiKey: "  ");

        // Act
        bool result = settings.IsTvdbConfigured;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsTvdbConfigured_ReturnsTrue_WhenTvdbApiKeyIsSet()
    {
        // Arrange
        RuvarrSettings settings = new(TvdbApiKey: "some-key");

        // Act
        bool result = settings.IsTvdbConfigured;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsTmdbConfigured_ReturnsFalse_WhenTmdbApiKeyIsEmpty()
    {
        // Arrange
        RuvarrSettings settings = new(TmdbApiKey: "");

        // Act
        bool result = settings.IsTmdbConfigured;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsTmdbConfigured_ReturnsFalse_WhenTmdbApiKeyIsWhitespace()
    {
        // Arrange
        RuvarrSettings settings = new(TmdbApiKey: "  ");

        // Act
        bool result = settings.IsTmdbConfigured;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsTmdbConfigured_ReturnsTrue_WhenTmdbApiKeyIsSet()
    {
        // Arrange
        RuvarrSettings settings = new(TmdbApiKey: "some-key");

        // Act
        bool result = settings.IsTmdbConfigured;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsSonarrConfigured_ReturnsFalse_WhenBothEmpty()
    {
        // Arrange
        RuvarrSettings settings = new(SonarrBaseAddress: "", SonarrApiKey: "");

        // Act
        bool result = settings.IsSonarrConfigured;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSonarrConfigured_ReturnsFalse_WhenBaseAddressIsEmpty()
    {
        // Arrange
        RuvarrSettings settings = new(SonarrBaseAddress: "", SonarrApiKey: "key");

        // Act
        bool result = settings.IsSonarrConfigured;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSonarrConfigured_ReturnsFalse_WhenApiKeyIsEmpty()
    {
        // Arrange
        RuvarrSettings settings = new(SonarrBaseAddress: "http://localhost:8989", SonarrApiKey: "");

        // Act
        bool result = settings.IsSonarrConfigured;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSonarrConfigured_ReturnsFalse_WhenBaseAddressIsWhitespace()
    {
        // Arrange
        RuvarrSettings settings = new(SonarrBaseAddress: "  ", SonarrApiKey: "key");

        // Act
        bool result = settings.IsSonarrConfigured;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSonarrConfigured_ReturnsTrue_WhenBothSet()
    {
        // Arrange
        RuvarrSettings settings = new(SonarrBaseAddress: "http://localhost:8989", SonarrApiKey: "key");

        // Act
        bool result = settings.IsSonarrConfigured;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsDownloadsConfigured_ReturnsFalse_WhenDownloadsRootDirectoryIsEmpty()
    {
        // Arrange
        RuvarrSettings settings = new(DownloadsRootDirectory: "");

        // Act
        bool result = settings.IsDownloadsConfigured;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsDownloadsConfigured_ReturnsFalse_WhenDownloadsRootDirectoryIsWhitespace()
    {
        // Arrange
        RuvarrSettings settings = new(DownloadsRootDirectory: "  ");

        // Act
        bool result = settings.IsDownloadsConfigured;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsDownloadsConfigured_ReturnsTrue_WhenDownloadsRootDirectoryIsSet()
    {
        // Arrange
        RuvarrSettings settings = new(DownloadsRootDirectory: "/downloads");

        // Act
        bool result = settings.IsDownloadsConfigured;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsReady_ReturnsTrue_WhenAllSettingsAreConfigured()
    {
        // Arrange
        RuvarrSettings settings = new(
            SonarrBaseAddress: "http://localhost:8989",
            SonarrApiKey: "key",
            TvdbApiKey: "tvdb-key",
            TmdbApiKey: "tmdb-key",
            DownloadsRootDirectory: "/downloads");

        // Act
        bool result = settings.IsReady;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsReady_ReturnsFalse_WhenTvdbIsNotConfigured()
    {
        // Arrange
        RuvarrSettings settings = new(
            SonarrBaseAddress: "http://localhost:8989",
            SonarrApiKey: "key",
            TvdbApiKey: "",
            TmdbApiKey: "tmdb-key",
            DownloadsRootDirectory: "/downloads");

        // Act
        bool result = settings.IsReady;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsReady_ReturnsFalse_WhenTmdbIsNotConfigured()
    {
        // Arrange
        RuvarrSettings settings = new(
            SonarrBaseAddress: "http://localhost:8989",
            SonarrApiKey: "key",
            TvdbApiKey: "tvdb-key",
            TmdbApiKey: "",
            DownloadsRootDirectory: "/downloads");

        // Act
        bool result = settings.IsReady;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsReady_ReturnsFalse_WhenSonarrIsNotConfigured()
    {
        // Arrange
        RuvarrSettings settings = new(
            SonarrBaseAddress: "",
            SonarrApiKey: "",
            TvdbApiKey: "tvdb-key",
            TmdbApiKey: "tmdb-key",
            DownloadsRootDirectory: "/downloads");

        // Act
        bool result = settings.IsReady;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsReady_ReturnsFalse_WhenDownloadsIsNotConfigured()
    {
        // Arrange
        RuvarrSettings settings = new(
            SonarrBaseAddress: "http://localhost:8989",
            SonarrApiKey: "key",
            TvdbApiKey: "tvdb-key",
            TmdbApiKey: "tmdb-key",
            DownloadsRootDirectory: "");

        // Act
        bool result = settings.IsReady;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsReady_ReturnsFalse_WhenEmpty()
    {
        // Arrange
        RuvarrSettings settings = RuvarrSettings.Empty;

        // Act
        bool result = settings.IsReady;

        // Assert
        result.ShouldBeFalse();
    }
}
