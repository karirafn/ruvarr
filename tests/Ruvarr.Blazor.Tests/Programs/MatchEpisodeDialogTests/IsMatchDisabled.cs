using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.Blazor.Tests.Programs.MatchEpisodeDialogTests;

public sealed class IsMatchDisabled
{
    [Fact]
    public void ReturnsTrueWhenInputIsEmpty()
    {
        // Arrange
        string input = string.Empty;

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(input, currentTvdbId: null, isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-100")]
    public void ReturnsTrueWhenParsedValueIsNotPositive(string input)
    {
        // Arrange
        // (input provided via theory)

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(input, currentTvdbId: null, isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenValidPositiveIntegerAndCurrentTvdbIdIsNull()
    {
        // Arrange
        string input = "12345";

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(input, currentTvdbId: null, isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueWhenParsedValueEqualsCurrentTvdbId()
    {
        // Arrange
        string input = "12345";

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(input, currentTvdbId: 12345, isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenParsedValueDiffersFromCurrentTvdbId()
    {
        // Arrange
        string input = "12345";

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(input, currentTvdbId: 99999, isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueWhenIsSubmittingRegardlessOfInput()
    {
        // Arrange
        string input = "12345";

        // Act
        bool result = MatchEpisodeDialog.IsMatchDisabled(input, currentTvdbId: null, isSubmitting: true);

        // Assert
        result.ShouldBeTrue();
    }
}
