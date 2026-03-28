using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class IsGenericEpisodeTitle
{
    [Theory]
    [InlineData("Þáttur 1 af 6")]
    [InlineData("Þáttur 4 af 6")]
    [InlineData("þáttur 10 af 12")]
    [InlineData("Þáttur 99 af 100")]
    public void ReturnsTrueForGenericTitles(string title)
    {
        // Arrange / Act
        bool result = RuvProgram.IsGenericEpisodeTitle(title);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Kastljós")]
    [InlineData("Þáttur um eitthvað")]
    [InlineData("Þáttur 1")]
    [InlineData("")]
    [InlineData("1 af 6")]
    [InlineData("Þáttur x af y")]
    public void ReturnsFalseForNonGenericTitles(string title)
    {
        // Arrange / Act
        bool result = RuvProgram.IsGenericEpisodeTitle(title);

        // Assert
        result.ShouldBeFalse();
    }
}
