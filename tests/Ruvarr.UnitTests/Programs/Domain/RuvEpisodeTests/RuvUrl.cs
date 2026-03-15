using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class RuvUrl
{
    [Fact]
    public void ReturnsNullWhenProgramSlugIsNull()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1001)
            .WithSlug(null)
            .Build();

        // Act
        RuvEpisode episode = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithRuvId("abc123")
            .Build();

        // Assert
        episode.RuvUrl.ShouldBeNull();
    }

    [Fact]
    public void ReturnsNullWhenProgramSlugIsEmpty()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1002)
            .WithSlug(string.Empty)
            .Build();

        // Act
        RuvEpisode episode = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithRuvId("abc123")
            .Build();

        // Assert
        episode.RuvUrl.ShouldBeNull();
    }

    [Fact]
    public void ReturnsUrlWhenSlugIsPresent()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1003)
            .WithSlug("frettir")
            .Build();

        // Act
        RuvEpisode episode = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithRuvId("abc123")
            .Build();

        // Assert
        episode.RuvUrl.ShouldBe(new Uri("https://www.ruv.is/sjonvarp/spila/frettir/1003/abc123"));
    }
}
