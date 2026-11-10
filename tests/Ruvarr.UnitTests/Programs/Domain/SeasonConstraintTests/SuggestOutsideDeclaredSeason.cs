using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.SeasonConstraintTests;

public sealed class SuggestOutsideDeclaredSeason
{
    private static readonly int[] YearSeasons = [1998, 1999, 2000];
    private static readonly int[] RegularSeasons = [1, 2, 3, 4, 5];

    private static Episode Candidate(int id, int season) =>
        new TvdbEpisodeDataBuilder().WithId(id).WithSeasonNumber(season).Build();

    [Fact]
    public void Strict_ReturnsTheOnlyCandidateOutsideTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1999, YearSeasons);

        // Act & Assert
        sut.SuggestOutsideDeclaredSeason([Candidate(201, 2000)])!.Id.ShouldBe(201);
    }

    [Fact]
    public void Preferred_ReturnsTheOnlyCandidateOutsideTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(5, RegularSeasons);

        // Act & Assert
        sut.SuggestOutsideDeclaredSeason([Candidate(304, 3)])!.Id.ShouldBe(304);
    }

    [Fact]
    public void ReturnsNothingWhenSeveralCandidatesSitOutsideTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1999, YearSeasons);

        // Act & Assert
        sut.SuggestOutsideDeclaredSeason([Candidate(201, 1998), Candidate(202, 2000)]).ShouldBeNull();
    }

    [Fact]
    public void ReturnsNothingWhenACandidateSitsInTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1999, YearSeasons);

        // Act & Assert
        sut.SuggestOutsideDeclaredSeason([Candidate(201, 1999), Candidate(202, 2000)]).ShouldBeNull();
    }

    [Fact]
    public void ReturnsNothingWithoutADeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(0, RegularSeasons);

        // Act & Assert
        sut.SuggestOutsideDeclaredSeason([Candidate(201, 2)]).ShouldBeNull();
    }

    [Fact]
    public void ReturnsNothingWithoutCandidates()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(5, RegularSeasons);

        // Act & Assert
        sut.SuggestOutsideDeclaredSeason([]).ShouldBeNull();
    }
}
