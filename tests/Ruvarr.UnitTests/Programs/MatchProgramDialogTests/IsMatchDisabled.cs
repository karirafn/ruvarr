using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.MatchProgramDialogTests;

public sealed class IsMatchDisabled
{
    [Fact]
    public void ReturnsTrueWhenInputIsEmpty()
    {
        // Arrange
        string input = string.Empty;

        // Act
        bool result = MatchProgramDialog.IsMatchDisabled(input, currentTvdbSeriesId: null, isSubmitting: false);

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
        bool result = MatchProgramDialog.IsMatchDisabled(input, currentTvdbSeriesId: null, isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenValidPositiveIntegerAndCurrentTvdbSeriesIdIsNull()
    {
        // Arrange
        string input = "12345";

        // Act
        bool result = MatchProgramDialog.IsMatchDisabled(input, currentTvdbSeriesId: null, isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueWhenParsedValueEqualsCurrentTvdbSeriesId()
    {
        // Arrange
        string input = "12345";

        // Act
        bool result = MatchProgramDialog.IsMatchDisabled(input, currentTvdbSeriesId: 12345, isSubmitting: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenParsedValueDiffersFromCurrentTvdbSeriesId()
    {
        // Arrange
        string input = "12345";

        // Act
        bool result = MatchProgramDialog.IsMatchDisabled(input, currentTvdbSeriesId: 99999, isSubmitting: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueWhenIsSubmittingRegardlessOfInput()
    {
        // Arrange
        string input = "12345";

        // Act
        bool result = MatchProgramDialog.IsMatchDisabled(input, currentTvdbSeriesId: null, isSubmitting: true);

        // Assert
        result.ShouldBeTrue();
    }
}
