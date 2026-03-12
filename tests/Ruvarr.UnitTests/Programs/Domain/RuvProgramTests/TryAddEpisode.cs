using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class TryAddEpisode
{
    [Fact]
    public void ReturnsTrueAndAddsEpisodeWhenIdIsNew()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();

        // Act
        bool result = sut.TryAddEpisode("ep1", new Uri("http://test.com"), "Title", "Desc", DateTime.UtcNow);

        // Assert
        result.ShouldBeTrue();
        sut.Episodes.ShouldHaveSingleItem();
    }

    [Fact]
    public void ReturnsFalseAndDoesNotAddWhenIdAlreadyExists()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "Title", "Desc", DateTime.UtcNow);

        // Act
        bool result = sut.TryAddEpisode("ep1", new Uri("http://test.com"), "Other Title", "Desc", DateTime.UtcNow);

        // Assert
        result.ShouldBeFalse();
        sut.Episodes.ShouldHaveSingleItem();
    }
}
