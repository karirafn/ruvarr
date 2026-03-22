using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class MatchMultiple
{
    [Fact]
    public void AddsMultipleTvdbEpisodes()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        List<TvdbEpisode> matches =
        [
            TvdbEpisode.Create(100, 1, 1, false),
            TvdbEpisode.Create(101, 1, 2, false),
        ];

        // Act
        sut.MatchMultiple(matches);

        // Assert
        sut.TvdbEpisodes.Count.ShouldBe(2);
        sut.TvdbEpisodes[0].TvdbId.ShouldBe(100);
        sut.TvdbEpisodes[1].TvdbId.ShouldBe(101);
    }

    [Fact]
    public void ReplacesExistingMatches()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);

        List<TvdbEpisode> newMatches =
        [
            TvdbEpisode.Create(200, 2, 1, false),
            TvdbEpisode.Create(201, 2, 2, false),
        ];

        // Act
        sut.MatchMultiple(newMatches);

        // Assert
        sut.TvdbEpisodes.Count.ShouldBe(2);
        sut.TvdbEpisodes[0].TvdbId.ShouldBe(200);
    }

    [Fact]
    public void SetsMatchedTimestamp()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        DateTime before = DateTime.UtcNow;

        // Act
        sut.MatchMultiple([TvdbEpisode.Create(100, 1, 1, false)]);

        // Assert
        sut.Matched.ShouldNotBeNull();
        sut.Matched.Value.ShouldBeInRange(before, DateTime.UtcNow);
    }

    [Fact]
    public void ClearsNextLookup()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.ScheduleLookup();

        // Act
        sut.MatchMultiple([TvdbEpisode.Create(100, 1, 1, false)]);

        // Assert
        sut.NextLookup.ShouldBeNull();
    }

    [Fact]
    public void RaisesEpisodeMatchedEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.MatchMultiple([TvdbEpisode.Create(100, 1, 1, false)]);

        // Assert
        sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<EpisodeMatchedEvent>();
    }

    [Fact]
    public void RaisesEpisodeMissingEvent_WhenAnyMatchIsMissing()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.MatchMultiple(
        [
            TvdbEpisode.Create(100, 1, 1, false),
            TvdbEpisode.Create(101, 1, 2, true),
        ]);

        // Assert
        sut.DomainEvents.Count.ShouldBe(2);
        sut.DomainEvents[0].ShouldBeOfType<EpisodeMatchedEvent>();
        sut.DomainEvents[1].ShouldBeOfType<EpisodeMissingEvent>();
    }

    [Fact]
    public void DoesNotRaiseEpisodeMissingEvent_WhenNoMatchIsMissing()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.MatchMultiple(
        [
            TvdbEpisode.Create(100, 1, 1, false),
            TvdbEpisode.Create(101, 1, 2, false),
        ]);

        // Assert
        sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<EpisodeMatchedEvent>();
    }
}
