using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class DomainEvents
{
    [Fact]
    public void SetMissing_WhenTransitioningToTrue_RaisesEpisodeMissingEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.SetMissing(true);

        // Assert
        EpisodeMissingEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<EpisodeMissingEvent>();
        @event.Episode.ShouldBe(sut);
    }

    [Fact]
    public void SetMissing_WhenAlreadyMissing_DoesNotRaiseSecondEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.SetMissing(true);
        sut.ClearDomainEvents();

        // Act
        sut.SetMissing(true);

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void SetMissing_WhenFalse_DoesNotRaiseEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.SetMissing(false);

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_ClearsAllEvents()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.SetMissing(true);

        // Act
        sut.ClearDomainEvents();

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Match_WhenIsMissingTrue_RaisesEpisodeMissingEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: true);

        // Assert
        EpisodeMissingEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<EpisodeMissingEvent>();
        @event.Episode.ShouldBe(sut);
    }

    [Fact]
    public void Match_WhenIsMissingFalse_DoesNotRaiseEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }
}
