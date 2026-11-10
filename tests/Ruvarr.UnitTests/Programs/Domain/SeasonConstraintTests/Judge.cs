using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.SeasonConstraintTests;

public sealed class Judge
{
    private static readonly int[] YearSeasons = [1987, 1998, 2024];
    private static readonly int[] RegularSeasons = [1, 2, 3, 4];

    private static Episode Candidate(int id, int season) =>
        new TvdbEpisodeDataBuilder().WithId(id).WithSeasonNumber(season).Build();

    private static TvdbEpisode Link(int id, int season) => TvdbEpisode.Create(id, season, 1);

    [Fact]
    public void Strict_RejectsALinkOutsideTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1999, YearSeasons);

        // Act & Assert
        sut.Judge(Link(2024, 2024), [Candidate(2024, 2024)]).ShouldBe(MatchVerdict.Rejected);
    }

    [Fact]
    public void Strict_KeepsALinkInTheDeclaredSeason()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1998, YearSeasons);

        // Act & Assert
        sut.Judge(Link(1998, 1998), [Candidate(1998, 1998)]).ShouldBe(MatchVerdict.Kept);
    }

    [Fact]
    public void Preferred_RejectsALinkWhenSelectPicksADifferentEpisode()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(4, RegularSeasons);

        // Act & Assert
        sut.Judge(Link(301, 3), [Candidate(301, 3), Candidate(401, 4)]).ShouldBe(MatchVerdict.Rejected);
    }

    [Fact]
    public void Preferred_KeepsALinkWhenSelectPicksTheSameEpisode()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1, RegularSeasons);

        // Act & Assert
        sut.Judge(Link(301, 3), [Candidate(301, 3)]).ShouldBe(MatchVerdict.Kept);
    }

    [Fact]
    public void Preferred_KeepsALinkWithoutCandidates()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(1, RegularSeasons);

        // Act & Assert
        sut.Judge(Link(301, 3), []).ShouldBe(MatchVerdict.Kept);
    }

    [Fact]
    public void Preferred_KeepsALinkWhenTheCandidatesAreAmbiguous()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(0, YearSeasons);

        // Act & Assert
        sut.Judge(Link(2026, 2026), [Candidate(1990, 1990), Candidate(2026, 2026)]).ShouldBe(MatchVerdict.Kept);
    }

    [Fact]
    public void YearDeclaredSeasonInASeriesWithoutYearSeasons_KeepsTheOffSeasonLink()
    {
        // Arrange
        SeasonConstraint sut = SeasonConstraint.Resolve(2024, [1, 32]);

        // Act & Assert
        sut.Judge(Link(3201, 32), [Candidate(3201, 32)]).ShouldBe(MatchVerdict.Kept);
    }
}
