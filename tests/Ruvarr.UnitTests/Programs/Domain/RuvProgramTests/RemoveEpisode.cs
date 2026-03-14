using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class RemoveEpisode
{
    [Fact]
    public void RemovesEpisodeFromList()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "Title", "Desc", DateTime.UtcNow);
        RuvEpisode episode = sut.Episodes[0];

        // Act
        sut.RemoveEpisode(episode);

        // Assert
        sut.Episodes.ShouldBeEmpty();
    }
}
