using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class TryGetEpisodeNumber
{
    [Fact]
    public void ReturnsTrueAndNumberForValidTitle()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().WithTitle("þáttur 3").Build();

        // Act
        bool result = sut.TryGetEpisodeNumber(out int number);

        // Assert
        result.ShouldBeTrue();
        number.ShouldBe(3);
    }

    [Fact]
    public void ReturnsTrueAndNumberCaseInsensitive()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().WithTitle("Þáttur 7").Build();

        // Act
        bool result = sut.TryGetEpisodeNumber(out int number);

        // Assert
        result.ShouldBeTrue();
        number.ShouldBe(7);
    }

    [Fact]
    public void ReturnsFalseForNonMatchingTitle()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().WithTitle("Mynd").Build();

        // Act
        bool result = sut.TryGetEpisodeNumber(out int number);

        // Assert
        result.ShouldBeFalse();
        number.ShouldBe(0);
    }

    [Fact]
    public void ReturnsFalseWhenSecondTokenIsNotInteger()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().WithTitle("þáttur eitt").Build();

        // Act
        bool result = sut.TryGetEpisodeNumber(out int number);

        // Assert
        result.ShouldBeFalse();
        number.ShouldBe(0);
    }

    [Theory]
    [InlineData("Þáttur 3 of 8", 3)]
    [InlineData("Þáttur 1 of 10", 1)]
    public void ReturnsTrueAndNumberForTitleWithOfSuffix(string title, int expectedNumber)
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().WithTitle(title).Build();

        // Act
        bool result = sut.TryGetEpisodeNumber(out int number);

        // Assert
        result.ShouldBeTrue();
        number.ShouldBe(expectedNumber);
    }

    [Fact]
    public void ReturnsFalseForEmptyTitle()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().WithTitle("").Build();

        // Act
        bool result = sut.TryGetEpisodeNumber(out int number);

        // Assert
        result.ShouldBeFalse();
        number.ShouldBe(0);
    }
}
