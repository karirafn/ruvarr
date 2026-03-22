using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class DomainEvents
{
    [Fact]
    public void UpdateMissingStatus_WhenTransitioningToTrue_RaisesEpisodeMissingEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);
        sut.ClearDomainEvents();

        // Act
        sut.UpdateMissingStatus(new HashSet<int> { 1 });

        // Assert
        EpisodeMissingEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<EpisodeMissingEvent>();
        @event.Episode.ShouldBe(sut);
    }

    [Fact]
    public void UpdateMissingStatus_WhenAlreadyMissing_DoesNotRaiseSecondEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: true);
        sut.ClearDomainEvents();

        // Act
        sut.UpdateMissingStatus(new HashSet<int> { 1 });

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateMissingStatus_WhenFalse_DoesNotRaiseEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);
        sut.ClearDomainEvents();

        // Act
        sut.UpdateMissingStatus([]);

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_ClearsAllEvents()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: true);

        // Act
        sut.ClearDomainEvents();

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Match_RaisesEpisodeMatchedEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);

        // Assert
        EpisodeMatchedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<EpisodeMatchedEvent>();
        @event.Episode.ShouldBe(sut);
    }

    [Fact]
    public void Match_WhenIsMissingTrue_RaisesBothEvents()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: true);

        // Assert
        sut.DomainEvents.Count.ShouldBe(2);
        sut.DomainEvents[0].ShouldBeOfType<EpisodeMatchedEvent>().Episode.ShouldBe(sut);
        sut.DomainEvents[1].ShouldBeOfType<EpisodeMissingEvent>().Episode.ShouldBe(sut);
    }

    [Fact]
    public void Match_OnRematch_RaisesEpisodeMatchedEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);
        sut.ClearDomainEvents();

        // Act
        sut.Match(tvdbId: 2, season: 2, episode: 5, isMissing: false);

        // Assert
        EpisodeMatchedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<EpisodeMatchedEvent>();
        @event.Episode.ShouldBe(sut);
    }

    [Fact]
    public void ScheduleLookup_RaisesEpisodeLookupScheduledEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.ScheduleLookup();

        // Assert
        EpisodeLookupScheduledEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<EpisodeLookupScheduledEvent>();
        @event.Episode.ShouldBe(sut);
    }

    [Fact]
    public void ScheduleLookup_WhenAlreadyMatched_DoesNotRaiseEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);
        sut.ClearDomainEvents();

        // Act
        sut.ScheduleLookup();

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ScheduleLookup_OnSuccessiveCalls_RaisesEventEachTime()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.ScheduleLookup();
        sut.ScheduleLookup();
        sut.ScheduleLookup();

        // Assert
        sut.DomainEvents.Count.ShouldBe(3);
        sut.DomainEvents.ShouldAllBe(e => e is EpisodeLookupScheduledEvent);
    }
}
