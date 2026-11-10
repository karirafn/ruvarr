using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.SeasonConstraintTests;

public sealed class SelectCandidate
{
    private static readonly int[] YearSeasons = [1987, 1998, 2024];
    private static readonly int[] RegularSeasons = [1, 2, 3, 4];

    private static Episode Candidate(int id, int season) =>
        new TvdbEpisodeDataBuilder().WithId(id).WithSeasonNumber(season).Build();

    [Fact]
    public void ReturnsNothingWithoutCandidates()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(2, RegularSeasons);

        // Act & Assert
        sut.Select([]).ShouldBeNull();
    }

    [Fact]
    public void Preferred_ReturnsTheOnlyCandidateInTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(2, RegularSeasons);

        // Act & Assert
        sut.Select([Candidate(201, 2)])!.Id.ShouldBe(201);
    }

    [Fact]
    public void Preferred_ReturnsTheOnlyCandidateOutsideTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1, RegularSeasons);

        // Act & Assert
        sut.Select([Candidate(301, 3)])!.Id.ShouldBe(301);
    }

    [Fact]
    public void Preferred_TheCandidateInTheDeclaredSeasonWinsOverOthers()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(4, RegularSeasons);

        // Act
        Episode? result = sut.Select([Candidate(301, 3), Candidate(401, 4)]);

        // Assert
        result!.Id.ShouldBe(401);
    }

    [Fact]
    public void Preferred_IsAmbiguousWithSeveralCandidatesInTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(4, RegularSeasons);

        // Act & Assert
        sut.Select([Candidate(401, 4), Candidate(402, 4)]).ShouldBeNull();
    }

    [Fact]
    public void Preferred_FallsBackToTheSingleOtherSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1, RegularSeasons);

        // Act & Assert
        sut.Select([Candidate(301, 3)])!.SeasonNumber.ShouldBe(3);
    }

    [Fact]
    public void Preferred_IsAmbiguousWithCandidatesSpreadOverTwoOtherSeasons()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1, RegularSeasons);

        // Act & Assert
        sut.Select([Candidate(201, 2), Candidate(301, 3)]).ShouldBeNull();
    }

    [Fact]
    public void Preferred_IsAmbiguousWithSeveralCandidatesInOneOtherSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1, RegularSeasons);

        // Act & Assert
        sut.Select([Candidate(301, 3), Candidate(302, 3)]).ShouldBeNull();
    }

    [Fact]
    public void NoDeclaredSeason_ReturnsTheOnlyCandidate()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(0, YearSeasons);

        // Act & Assert
        sut.Select([Candidate(2026, 2026)])!.Id.ShouldBe(2026);
    }

    [Fact]
    public void NoDeclaredSeason_DoesNotPreferSpecials()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(0, YearSeasons);

        // Act & Assert
        sut.Select([Candidate(1, 0), Candidate(2026, 2026)]).ShouldBeNull();
    }

    [Fact]
    public void NoDeclaredSeason_IsAmbiguousWithCandidatesInTwoSeasons()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(0, YearSeasons);

        // Act & Assert
        sut.Select([Candidate(1990, 1990), Candidate(2024, 2024)]).ShouldBeNull();
    }

    [Fact]
    public void Strict_ReturnsTheCandidateInTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1998, YearSeasons);

        // Act
        Episode? result = sut.Select([Candidate(1998, 1998), Candidate(2024, 2024)]);

        // Assert
        result!.Id.ShouldBe(1998);
    }

    [Fact]
    public void Strict_DoesNotFallBackToTheOnlyCandidateOutsideTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1999, YearSeasons);

        // Act & Assert
        sut.Select([Candidate(2024, 2024)]).ShouldBeNull();
    }

    [Fact]
    public void Strict_IsAmbiguousWithSeveralCandidatesInTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1998, YearSeasons);

        // Act & Assert
        sut.Select([Candidate(1, 1998), Candidate(2, 1998)]).ShouldBeNull();
    }

    [Fact]
    public void BelowTheThreshold_FallsBackLikePreferred()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1899, YearSeasons);

        // Act & Assert
        sut.Select([Candidate(2024, 2024)])!.Id.ShouldBe(2024);
    }

    [Fact]
    public void YearDeclaredSeasonInASeriesWithoutYearSeasons_FallsBackLikePreferred()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(2024, [1, 32]);

        // Act & Assert
        sut.Select([Candidate(3201, 32)])!.Id.ShouldBe(3201);
    }
}
