using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class ResolveSeasonConstraint
{
    [Fact]
    public void YearNamedProgramInAYearNumberedSeries_IsStrict()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Gettu betur 1999").Build();

        // Act
        SeasonConstraint result = sut.ResolveSeasonConstraint([1987, 1998, 2003, 2026]);

        // Assert
        result.ShouldBe(SeasonConstraint.Resolve(1999, [2026]));
        result.IsStrict.ShouldBeTrue();
    }

    [Fact]
    public void YearNamedProgramInASequentiallyNumberedSeries_IsPreferred()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Söngvakeppnin 2024").Build();

        // Act
        SeasonConstraint result = sut.ResolveSeasonConstraint([1, 31, 32]);

        // Assert
        result.DeclaredSeason.ShouldBe(2024);
        result.IsStrict.ShouldBeFalse();
    }

    [Fact]
    public void RomanNumeralProgram_IsPreferredForThatSeason()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Andri og Edda I").Build();

        // Act
        SeasonConstraint result = sut.ResolveSeasonConstraint([1, 2, 3]);

        // Assert
        result.DeclaredSeason.ShouldBe(1);
        result.IsStrict.ShouldBeFalse();
    }

    [Fact]
    public void ProgramWithoutADeclaredSeason_IsPreferredWithNoPreference()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Gettu betur").Build();

        // Act
        SeasonConstraint result = sut.ResolveSeasonConstraint([1987, 2026]);

        // Assert
        result.DeclaredSeason.ShouldBe(0);
        result.IsStrict.ShouldBeFalse();
    }
}
