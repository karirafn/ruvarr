using System.Text;

using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.EpisodeComboboxTests;

public sealed class Matches
{
    [Fact]
    public void WhenQueryMatchesSeasonCode_ReturnsTrue()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Pilot", SeasonNumber: 2, EpisodeNumber: 5);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "s02");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenQueryMatchesEpisodeCode_ReturnsTrue()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Pilot", SeasonNumber: 2, EpisodeNumber: 5);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "e05");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenQueryMatchesFullCode_ReturnsTrue()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Pilot", SeasonNumber: 2, EpisodeNumber: 5);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "s02e05");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenQueryMatchesNameFragment_ReturnsTrue()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Breaking Bad", SeasonNumber: 1, EpisodeNumber: 1);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "breaking");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenNameIsNullAndQueryDoesNotMatchCode_DoesNotThrow()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: null!, SeasonNumber: 1, EpisodeNumber: 1);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "pilot");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void WhenQueryDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Pilot", SeasonNumber: 2, EpisodeNumber: 5);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "xyz999");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void WhenQueryIsNfdAndEpisodeNameIsNfc_ReturnsTrue()
    {
        // Arrange
        // NFD: o + \u0308 (COMBINING DIAERESIS) — decomposed form of o-umlaut.
        // Written as \uXXXX escapes so editor/git normalization cannot collapse them.
        string nfdQuery = "\u006f\u0308sterreich";
        nfdQuery.IsNormalized(NormalizationForm.FormC).ShouldBeFalse();

        // NFC: \u00f6 (o-umlaut, composed) — the form that flows from NfcStringConverter.
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "\u00f6sterreich", SeasonNumber: 1, EpisodeNumber: 1);

        // Act
        bool result = EpisodeCombobox.Matches(episode, nfdQuery);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenEpisodeNameIsNfdAndQueryIsNfc_ReturnsTrue()
    {
        // Arrange
        // NFD episode Name: o + \u0308 (COMBINING DIAERESIS) — decomposed form of o-umlaut.
        // Written as \uXXXX escapes so editor/git normalization cannot collapse them.
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "\u006f\u0308sterreich", SeasonNumber: 1, EpisodeNumber: 1);
        // NFC query: \u00f6 (o-umlaut, composed)
        string nfcQuery = "\u00f6sterreich";

        // Act
        bool result = EpisodeCombobox.Matches(episode, nfcQuery);

        // Assert
        episode.Name.IsNormalized(NormalizationForm.FormC).ShouldBeFalse();
        result.ShouldBeTrue();
    }
}
