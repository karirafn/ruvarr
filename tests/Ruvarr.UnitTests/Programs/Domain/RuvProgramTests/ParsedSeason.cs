using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class ParsedSeason
{
    [Fact]
    public void KeepsTheTokenTheSeasonWasParsedFrom()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Ráðherrann II").Build();

        // Act & Assert
        sut.ParsedSeason.ShouldBe(new Ruvarr.Programs.Domain.ParsedSeason("II", 2));
    }

    [Fact]
    public void FallsBackToTheForeignName()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Ráðherrann").WithForeignName("The Minister 3").Build();

        // Act & Assert
        sut.ParsedSeason.ShouldBe(new Ruvarr.Programs.Domain.ParsedSeason("3", 3));
    }

    [Fact]
    public void IsNullWhenNothingParses()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Ráðherrann").WithForeignName(null).Build();

        // Act & Assert
        sut.ParsedSeason.ShouldBeNull();
    }
}
