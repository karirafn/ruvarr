using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class SonarrStatus
{
    [Fact]
    public void IsNull_WhenUnmatched()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Assert
        sut.SonarrStatus.ShouldBeNull();
    }

    [Theory]
    [InlineData(new[] { SonarrEpisodeStatus.Downloaded }, SonarrEpisodeStatus.Downloaded)]
    [InlineData(new[] { SonarrEpisodeStatus.Missing }, SonarrEpisodeStatus.Missing)]
    [InlineData(new[] { SonarrEpisodeStatus.NotTracked }, SonarrEpisodeStatus.NotTracked)]
    [InlineData(new[] { SonarrEpisodeStatus.Downloaded, SonarrEpisodeStatus.Downloaded }, SonarrEpisodeStatus.Downloaded)]
    [InlineData(new[] { SonarrEpisodeStatus.Downloaded, SonarrEpisodeStatus.Missing }, SonarrEpisodeStatus.Missing)]
    [InlineData(new[] { SonarrEpisodeStatus.Downloaded, SonarrEpisodeStatus.NotTracked }, SonarrEpisodeStatus.NotTracked)]
    [InlineData(new[] { SonarrEpisodeStatus.NotTracked, SonarrEpisodeStatus.Missing }, SonarrEpisodeStatus.Missing)]
    public void CombinesLinkStatuses(SonarrEpisodeStatus[] linkStatuses, SonarrEpisodeStatus expected)
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        Dictionary<int, SonarrEpisodeStatus> lookup = [];
        List<TvdbEpisode> links = [];
        for (int i = 0; i < linkStatuses.Length; i++)
        {
            lookup[i + 1] = linkStatuses[i];
            links.Add(TvdbEpisode.Create(i + 1, 1, i + 1));
        }

        // Act
        sut.MatchMultiple(links, SonarrEpisodeStatuses.From(lookup));

        // Assert
        sut.SonarrStatus.ShouldBe(expected);
    }
}