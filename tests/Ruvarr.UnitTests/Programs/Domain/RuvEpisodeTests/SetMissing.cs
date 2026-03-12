using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class SetMissing
{
    [Fact]
    public void IsFalseByDefault()
    {
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        sut.IsMissing.ShouldBeFalse();
    }

    [Fact]
    public void SetsTrueWhenMissing()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.SetMissing(true);

        // Assert
        sut.IsMissing.ShouldBeTrue();
    }

    [Fact]
    public void SetsFalseWhenNotMissing()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.SetMissing(true);

        // Act
        sut.SetMissing(false);

        // Assert
        sut.IsMissing.ShouldBeFalse();
    }
}
