using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.SeasonConstraintTests;

public sealed class Resolve
{
    [Theory]
    [InlineData(1900, true)]
    [InlineData(1999, true)]
    [InlineData(1899, false)]
    [InlineData(4, false)]
    [InlineData(0, false)]
    public void IsStrictOnlyForAYearDeclaredSeasonInAYearNumberedSeries(int declaredSeason, bool expectedStrict)
    {
        // Act
        SeasonConstraint sut = SeasonConstraint.Resolve(declaredSeason, [1, 1987, 2024]);

        // Assert
        sut.DeclaredSeason.ShouldBe(declaredSeason);
        sut.IsStrict.ShouldBe(expectedStrict);
    }

    [Fact]
    public void IsPreferredForAYearDeclaredSeasonInASeriesWithoutYearSeasons()
    {
        // Act
        SeasonConstraint sut = SeasonConstraint.Resolve(2024, [1, 2, 32]);

        // Assert
        sut.IsStrict.ShouldBeFalse();
    }

    [Fact]
    public void IsStrictWhenTheSeriesOnlyHasASeasonAtTheThreshold()
    {
        // Act
        SeasonConstraint sut = SeasonConstraint.Resolve(2000, [1900]);

        // Assert
        sut.IsStrict.ShouldBeTrue();
    }

    [Fact]
    public void IsPreferredWhenTheSeriesHighestSeasonIsBelowTheThreshold()
    {
        // Act
        SeasonConstraint sut = SeasonConstraint.Resolve(2000, [1899]);

        // Assert
        sut.IsStrict.ShouldBeFalse();
    }

    [Fact]
    public void RejectsANegativeDeclaredSeason()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => SeasonConstraint.Resolve(-1, [1]));
    }
}
