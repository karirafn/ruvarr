using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.MatchEpisodeDialogTests;

public sealed class IsMatchDisabled
{
    [Fact]
    public void ReturnsTrueWhenSelectedEpisodeIdIsNull()
    {
        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(selectedEpisodeId: null, currentTvdbId: null, isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenSelectedEpisodeIdIsSetAndCurrentTvdbIdIsNull()
    {
        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(selectedEpisodeId: 12345, currentTvdbId: null, isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueWhenSelectedEpisodeIdEqualsCurrentTvdbId()
    {
        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(selectedEpisodeId: 12345, currentTvdbId: 12345, isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenSelectedEpisodeIdDiffersFromCurrentTvdbId()
    {
        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(selectedEpisodeId: 12345, currentTvdbId: 99999, isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueWhenIsSubmittingRegardlessOfSelection()
    {
        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(selectedEpisodeId: 12345, currentTvdbId: null, isSubmitting: true);

        // Assert
        result.ShouldBeTrue();
    }
}
