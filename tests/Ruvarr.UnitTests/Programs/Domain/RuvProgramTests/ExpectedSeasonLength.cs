using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class ExpectedSeasonLength
{
    private static RuvProgram ProgramWithTitles(params string[] titles)
    {
        RuvProgram program = new RuvProgramBuilder().WithName("Molang VI").Build();
        for (int i = 0; i < titles.Length; i++)
        {
            program.TryAddEpisode($"ep{i:D4}", new Uri("http://test.com"), titles[i], "", DateTime.UtcNow, TimeSpan.FromMinutes(7));
        }

        return program;
    }

    [Fact]
    public void IsTheMOfGenericTitlesWhenTheyAllAgree()
    {
        // Arrange
        RuvProgram sut = ProgramWithTitles("Þáttur 1 af 52", "Þáttur 2 af 52", "þáttur 3 af 52");

        // Act & Assert
        sut.ExpectedSeasonLength.ShouldBe(52);
    }

    [Fact]
    public void IsThePublishedCountWhenGenericTitlesDisagree()
    {
        // Arrange
        RuvProgram sut = ProgramWithTitles("Þáttur 1 af 52", "Þáttur 2 af 26");

        // Act & Assert
        sut.ExpectedSeasonLength.ShouldBe(2);
    }

    [Fact]
    public void IsThePublishedCountWhenTitlesHaveNoM()
    {
        // Arrange
        RuvProgram sut = ProgramWithTitles("Þáttur 1", "Þáttur 2", "Þáttur 3");

        // Act & Assert
        sut.ExpectedSeasonLength.ShouldBe(3);
    }

    [Fact]
    public void IgnoresDescriptiveTitlesWhenTheGenericOnesAgree()
    {
        // Arrange
        RuvProgram sut = ProgramWithTitles("Þáttur 1 af 6", "Jólaþáttur");

        // Act & Assert
        sut.ExpectedSeasonLength.ShouldBe(6);
    }

    [Fact]
    public void IsZeroForAProgramWithoutEpisodes()
    {
        // Arrange
        RuvProgram sut = ProgramWithTitles();

        // Act & Assert
        sut.ExpectedSeasonLength.ShouldBe(0);
    }
}
