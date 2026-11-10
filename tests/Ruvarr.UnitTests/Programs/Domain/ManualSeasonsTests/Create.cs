using Ruvarr.Abstractions;
using Ruvarr.Programs;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.ManualSeasonsTests;

public sealed class Create
{
    [Fact]
    public void KeepsDistinctSeasonsInAscendingOrder()
    {
        // Act
        Result<ManualSeasons> result = ManualSeasons.Create([3, 1, 3]);

        // Assert
        result.FromResult().Numbers.ShouldBe([1, 3]);
    }

    [Fact]
    public void RejectsAnEmptySet()
    {
        // Act
        Result<ManualSeasons> result = ManualSeasons.Create([]);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ManualSeasonsEmpty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsANonRegularSeason(int season)
    {
        // Act
        Result<ManualSeasons> result = ManualSeasons.Create([1, season]);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ManualSeasonNotInSeries);
    }

    [Fact]
    public void EqualsAnotherSetWithTheSameSeasons()
    {
        // Act & Assert
        ManualSeasons.Create([2, 1]).FromResult().ShouldBe(ManualSeasons.Create([1, 2]).FromResult());
    }

    [Fact]
    public void RoundTripsThroughItsStoredForm()
    {
        // Arrange
        ManualSeasons sut = ManualSeasons.Create([1, 12]).FromResult();

        // Act
        ManualSeasons result = ManualSeasons.FromStorage(sut.ToStorage());

        // Assert
        result.ShouldBe(sut);
    }
}
