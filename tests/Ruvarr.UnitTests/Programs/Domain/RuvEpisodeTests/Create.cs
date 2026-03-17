using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class Create
{
    [Fact]
    public void SetsProgram()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().Build();

        // Act
        RuvEpisode result = new RuvEpisodeBuilder().WithProgram(program).Build();

        // Assert
        result.Program.ShouldBe(program);
    }

    [Fact]
    public void SetsRuvId()
    {
        // Arrange / Act
        RuvEpisode result = new RuvEpisodeBuilder().WithRuvId("abc123").Build();

        // Assert
        result.RuvId.ShouldBe("abc123");
    }

    [Fact]
    public void SetsUri()
    {
        // Arrange
        Uri uri = new("http://test.com/episode.mp4");

        // Act
        RuvEpisode result = new RuvEpisodeBuilder().WithUri(uri).Build();

        // Assert
        result.Uri.ShouldBe(uri);
    }

    [Fact]
    public void SetsTitle()
    {
        // Arrange / Act
        RuvEpisode result = new RuvEpisodeBuilder().WithTitle("Þáttur 1").Build();

        // Assert
        result.Title.ShouldBe("Þáttur 1");
    }

    [Fact]
    public void SetsDescription()
    {
        // Arrange / Act
        RuvEpisode result = new RuvEpisodeBuilder().WithDescription("A great episode").Build();

        // Assert
        result.Description.ShouldBe("A great episode");
    }

    [Fact]
    public void SetsFirstRun()
    {
        // Arrange
        DateTime firstRun = new(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        RuvEpisode result = new RuvEpisodeBuilder().WithFirstRun(firstRun).Build();

        // Assert
        result.FirstRun.ShouldBe(firstRun);
    }

    [Fact]
    public void SetsDurationSeconds()
    {
        // Arrange / Act
        RuvEpisode result = new RuvEpisodeBuilder().WithDurationSeconds(1800).Build();

        // Assert
        result.DurationSeconds.ShouldBe(1800);
    }

    [Fact]
    public void DurationSecondsDefaultsToNull()
    {
        // Arrange / Act
        RuvEpisode result = new RuvEpisodeBuilder().Build();

        // Assert
        result.DurationSeconds.ShouldBeNull();
    }

    [Fact]
    public void TvdbIdIsNull()
    {
        // Arrange / Act
        RuvEpisode result = new RuvEpisodeBuilder().Build();

        // Assert
        result.TvdbId.ShouldBeNull();
    }

    [Fact]
    public void DownloadQueueItemIsNull()
    {
        // Arrange / Act
        RuvEpisode result = new RuvEpisodeBuilder().Build();

        // Assert
        result.DownloadQueueItem.ShouldBeNull();
    }
}
