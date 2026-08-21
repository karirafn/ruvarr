using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.MatchEpisodeDialogTests;

public sealed class IsMatchDisabled
{
    [Fact]
    public void WhenNoEntries_ReturnsTrue()
    {
        // Arrange

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled([], [], isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenAnyEntryHasNoSelection_ReturnsTrue()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries = [new() { SelectedEpisodeId = null }];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, [], isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenAllEntriesSelectedAndNoCurrentMatches_ReturnsFalse()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries = [new() { SelectedEpisodeId = 12345 }];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, [], isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void WhenSelectionsMatchCurrentMatches_ReturnsTrue()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries = [new() { SelectedEpisodeId = 12345 }];
        List<TvdbEpisodeSummary> current = [new(12345, 1, 1, false, false, null)];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, current, isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenSelectionsDifferFromCurrentMatches_ReturnsFalse()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries = [new() { SelectedEpisodeId = 12345 }];
        List<TvdbEpisodeSummary> current = [new(99999, 1, 1, false, false, null)];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, current, isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void WhenIsSubmitting_ReturnsTrue()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries = [new() { SelectedEpisodeId = 12345 }];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, [], isSubmitting: true);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenTwoEntriesSelectSameEpisode_ReturnsTrue()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries =
        [
            new() { SelectedEpisodeId = 12345 },
            new() { SelectedEpisodeId = 12345 },
        ];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, [], isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenDifferentNumberOfMatches_ReturnsFalse()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries =
        [
            new() { SelectedEpisodeId = 100 },
            new() { SelectedEpisodeId = 101 },
        ];
        List<TvdbEpisodeSummary> current = [new(100, 1, 1, false, false, null)];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, current, isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }
}
