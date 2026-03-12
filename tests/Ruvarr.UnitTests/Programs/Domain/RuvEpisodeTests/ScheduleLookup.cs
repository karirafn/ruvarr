using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class ScheduleLookup
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 24)]
    [InlineData(5, 168)]
    public void SetsNextLookupWithExponentialBackoff(int callCount, int expectedHours)
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        DateTime before = DateTime.UtcNow;

        // Act
        for (int i = 0; i < callCount; i++)
        {
            sut.ScheduleLookup();
        }

        // Assert
        sut.NextLookup.ShouldNotBeNull();
        sut.NextLookup.Value.ShouldBeInRange(
            before.AddHours(expectedHours),
            DateTime.UtcNow.AddHours(expectedHours));
    }

    [Fact]
    public void IncrementsLookupCount()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.ScheduleLookup();

        // Assert
        sut.LookupCount.ShouldBe(1);
    }

    [Fact]
    public void DoesNothingWhenAlreadyMatched()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1);

        // Act
        sut.ScheduleLookup();

        // Assert
        sut.NextLookup.ShouldBeNull();
        sut.LookupCount.ShouldBe(0);
    }
}
