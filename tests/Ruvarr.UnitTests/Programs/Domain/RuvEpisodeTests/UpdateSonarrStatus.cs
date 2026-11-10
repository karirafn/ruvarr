using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class UpdateSonarrStatus
{
    private static RuvEpisode MatchedEpisode(SonarrEpisodeStatus status)
    {
        RuvEpisode episode = new RuvEpisodeBuilder().Build();
        episode.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.Of(1, status), origin: MatchOrigin.Automatic);
        episode.ClearDomainEvents();
        return episode;
    }

    private static Dictionary<int, SonarrEpisodeStatus> Lookup(SonarrEpisodeStatus status) => new() { [1] = status };

    [Theory]
    [InlineData(SonarrEpisodeStatus.Downloaded)]
    [InlineData(SonarrEpisodeStatus.Missing)]
    [InlineData(SonarrEpisodeStatus.NotTracked)]
    public void SetsLinkStatusFromLookup(SonarrEpisodeStatus status)
    {
        // Arrange
        RuvEpisode sut = MatchedEpisode(SonarrEpisodeStatus.NotTracked);

        // Act
        sut.UpdateSonarrStatus(Lookup(status));

        // Assert
        sut.TvdbEpisodes[0].SonarrStatus.ShouldBe(status);
    }

    [Fact]
    public void SetsNotTracked_WhenEpisodeAbsentFromLookup()
    {
        // Arrange
        RuvEpisode sut = MatchedEpisode(SonarrEpisodeStatus.Downloaded);

        // Act
        sut.UpdateSonarrStatus(new Dictionary<int, SonarrEpisodeStatus>());

        // Assert
        sut.TvdbEpisodes[0].SonarrStatus.ShouldBe(SonarrEpisodeStatus.NotTracked);
    }

    [Theory]
    [InlineData(SonarrEpisodeStatus.NotTracked)]
    [InlineData(SonarrEpisodeStatus.Downloaded)]
    public void TransitionIntoMissing_RaisesEpisodeMissingEvent(SonarrEpisodeStatus previous)
    {
        // Arrange
        RuvEpisode sut = MatchedEpisode(previous);

        // Act
        sut.UpdateSonarrStatus(Lookup(SonarrEpisodeStatus.Missing));

        // Assert
        sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<EpisodeMissingEvent>().Episode.ShouldBe(sut);
    }

    [Theory]
    [InlineData(SonarrEpisodeStatus.Missing, SonarrEpisodeStatus.Missing)]
    [InlineData(SonarrEpisodeStatus.Missing, SonarrEpisodeStatus.Downloaded)]
    [InlineData(SonarrEpisodeStatus.Missing, SonarrEpisodeStatus.NotTracked)]
    [InlineData(SonarrEpisodeStatus.NotTracked, SonarrEpisodeStatus.Downloaded)]
    [InlineData(SonarrEpisodeStatus.Downloaded, SonarrEpisodeStatus.NotTracked)]
    [InlineData(SonarrEpisodeStatus.NotTracked, SonarrEpisodeStatus.NotTracked)]
    public void OtherTransitions_RaiseNothing(SonarrEpisodeStatus previous, SonarrEpisodeStatus next)
    {
        // Arrange
        RuvEpisode sut = MatchedEpisode(previous);

        // Act
        sut.UpdateSonarrStatus(Lookup(next));

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void SecondLinkBecomingMissing_WhileFirstAlreadyMissing_RaisesNothing()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.MatchMultiple(
            [TvdbEpisode.Create(1, 1, 1), TvdbEpisode.Create(2, 1, 2)],
            SonarrStatuses.Missing(1),
            MatchOrigin.Manual);
        sut.ClearDomainEvents();

        // Act
        sut.UpdateSonarrStatus(new Dictionary<int, SonarrEpisodeStatus>
        {
            [1] = SonarrEpisodeStatus.Missing,
            [2] = SonarrEpisodeStatus.Missing,
        });

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }
}