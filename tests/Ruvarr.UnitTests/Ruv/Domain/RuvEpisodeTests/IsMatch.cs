using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Ruv.Domain.RuvEpisodeTests;

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
        string value = "Test Episode, Part 1";
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithTitle(title)
            .Build();

        // Act
        bool result = sut.IsMatch(value);

        // Assert
        result.ShouldBeTrue();
    }
}