using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class IsMatch
{
    [Theory]
    [InlineData("Test Episode, Part 1")]
    [InlineData("Test episode, Part 1")]
    [InlineData("Test Episode - part 1")]
    [InlineData("1. Test Episode, Part 1")]
    [InlineData("1. Test Episode - Part 1")]
    [InlineData("1, Test Episode - Part 1")]
    [InlineData("1. kafli: Test Episode, Part 1")]
    [InlineData("1. Kafli: Test Episode, Part 1")]
    [InlineData("1. þáttur: Test Episode, Part 1")]
    [InlineData("1. Þáttur: Test Episode, Part 1")]
    [InlineData("Þáttur 1: Test Episode, Part 1")]
    [InlineData("1.Test Episode, Part 1")]
    public void ReturnsTrueWhenTitleMatchesValue(string title)
    {
        // Arrange
        string value = "Test Episode, Part 1";
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithTitle(title)
            .Build();

        // Act
        bool result = sut.IsMatch(value);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenStoredTitleIsNfd_AndComparedValueIsNfc_ReturnsTrue()
    {
        // Arrange
        // NFD title uses \uXXXX C# escape sequences — the source file stores
        // literal backslash-u so git/editor normalization cannot collapse the
        // combining diacritics to NFC at save time.
        // ö (NFD) = o + U+0308 (combining diaeresis); á (NFD) = a + U+0301 (combining acute)
        const string NfdTitle = "Skjaldbo\u0308kustra\u0301kur";
        const string NfcValue = "Skjaldbökustrákur";
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithTitle(NfdTitle)
            .Build();

        // Act
        bool result = sut.IsMatch(NfcValue);

        // Assert
        result.ShouldBeTrue();
    }
}
