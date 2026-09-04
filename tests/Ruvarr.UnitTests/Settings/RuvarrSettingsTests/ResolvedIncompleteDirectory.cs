using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.RuvarrSettingsTests;

public sealed class ResolvedIncompleteDirectory
{
    [Fact]
    public void WhenDefaultRoot_ReturnsIncompleteUnderDownloads()
    {
        // Arrange
        RuvarrSettings settings = new();

        // Act
        string result = settings.ResolvedIncompleteDirectory;

        // Assert
        result.ShouldBe(Path.Join("/downloads", "incomplete"));
    }

    [Fact]
    public void WhenCustomDownloadsRoot_ReturnsIncompleteUnderCustomRoot()
    {
        // Arrange
        RuvarrSettings settings = new() { DownloadsRoot = "/media/downloads" };

        // Act
        string result = settings.ResolvedIncompleteDirectory;

        // Assert
        result.ShouldBe(Path.Join("/media/downloads", "incomplete"));
    }
}
