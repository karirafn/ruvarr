using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;

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

    [Fact]
    public void RaisesEpisodeAddedToMatchedProgramEventWhenSeriesIsMatched()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.MatchTvdb(TvdbSeries.Create(1, "Test Series"));
        sut.ClearDomainEvents();

        // Act
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "Title", "Desc", DateTime.UtcNow);

        // Assert
        sut.DomainEvents.ShouldContain(e => e is EpisodeAddedToMatchedProgramEvent);
        EpisodeAddedToMatchedProgramEvent raised = (EpisodeAddedToMatchedProgramEvent)sut.DomainEvents
            .Single(e => e is EpisodeAddedToMatchedProgramEvent);
        raised.RuvId.ShouldBe(sut.RuvId);
        raised.Name.ShouldBe(sut.Name);
    }

    [Fact]
    public void DoesNotRaiseEpisodeAddedToMatchedProgramEventWhenSeriesIsNotMatched()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.ClearDomainEvents();

        // Act
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "Title", "Desc", DateTime.UtcNow);

        // Assert
        sut.DomainEvents.ShouldNotContain(e => e is EpisodeAddedToMatchedProgramEvent);
    }

    [Fact]
    public void PassesDurationSecondsToEpisode()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();

        // Act
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "Title", "Desc", DateTime.UtcNow, durationSeconds: 2700);

        // Assert
        sut.Episodes.ShouldHaveSingleItem();
        sut.Episodes[0].DurationSeconds.ShouldBe(2700);
    }

    [Fact]
    public void DoesNotRaiseEpisodeAddedToMatchedProgramEventForDuplicateEpisode()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.MatchTvdb(TvdbSeries.Create(1, "Test Series"));
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "Title", "Desc", DateTime.UtcNow);
        sut.ClearDomainEvents();

        // Act
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "Title", "Desc", DateTime.UtcNow);

        // Assert
        sut.DomainEvents.ShouldNotContain(e => e is EpisodeAddedToMatchedProgramEvent);
    }
}
