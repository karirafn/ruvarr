using Ruvarr.Contracts;
using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.MatchEpisodeDialogTests;

public sealed class IsMatchDisabled
{
    [Fact]
    public void ReturnsTrueWhenNoEntries()
    {
        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled([], [], isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenAnyEntryHasNoSelection()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries = [new() { SelectedEpisodeId = null }];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, [], isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenAllEntriesSelectedAndNoCurrentMatches()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries = [new() { SelectedEpisodeId = 12345 }];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, [], isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueWhenSelectionsMatchCurrentMatches()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries = [new() { SelectedEpisodeId = 12345 }];
        List<TvdbEpisodeSummary> current = [new(12345, 1, 1, false, false)];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, current, isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenSelectionsDifferFromCurrentMatches()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries = [new() { SelectedEpisodeId = 12345 }];
        List<TvdbEpisodeSummary> current = [new(99999, 1, 1, false, false)];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, current, isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueWhenIsSubmitting()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries = [new() { SelectedEpisodeId = 12345 }];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, [], isSubmitting: true);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenDifferentNumberOfMatches()
    {
        // Arrange
        List<MatchEpisodeDialog.MatchEntry> entries =
        [
            new() { SelectedEpisodeId = 100 },
            new() { SelectedEpisodeId = 101 },
        ];
        List<TvdbEpisodeSummary> current = [new(100, 1, 1, false, false)];

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(entries, current, isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }
}
