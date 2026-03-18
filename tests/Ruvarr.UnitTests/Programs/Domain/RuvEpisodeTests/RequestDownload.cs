using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class RequestDownload
{
    [Fact]
    public void RaisesEpisodeDownloadRequestedEvent()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.RequestDownload();

        // Assert
        sut.DomainEvents.ShouldContain(e => e is EpisodeDownloadRequestedEvent);
    }

    [Fact]
    public void EventReferencesEpisode()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.RequestDownload();

        // Assert
        EpisodeDownloadRequestedEvent @event = sut.DomainEvents
            .OfType<EpisodeDownloadRequestedEvent>()
            .ShouldHaveSingleItem();
        @event.Episode.ShouldBe(sut);
    }
}
