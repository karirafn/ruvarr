using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.SeasonConstraintTests;

public sealed class StrictToManualSeasons
{
    private static Episode Candidate(int id, int season) =>
        new TvdbEpisodeDataBuilder().WithId(id).WithSeasonNumber(season).Build();

    private static TvdbEpisode Link(int id, int season) => TvdbEpisode.Create(id, season, 1);

    private static SeasonConstraint StrictTo(params int[] seasons) =>
        SeasonConstraint.StrictTo(ManualSeasons.Create(seasons).FromResult());

    [Fact]
    public void IsStrictToTheSet()
    {
        // Act
        SeasonConstraint sut = StrictTo(1, 2);

        // Assert
        sut.IsStrict.ShouldBeTrue();
        sut.DeclaredSeasons.ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public void Select_OnlyConsidersCandidatesInTheSet()
    {
        // Arrange
        SeasonConstraint sut = StrictTo(1, 2);

        // Act & Assert
        sut.Select([Candidate(201, 2), Candidate(301, 3)])!.Id.ShouldBe(201);
    }

    [Fact]
    public void Select_DoesNotFallBackToTheOnlyCandidateOutsideTheSet()
    {
        // Arrange
        SeasonConstraint sut = StrictTo(2);

        // Act & Assert
        sut.Select([Candidate(301, 3)]).ShouldBeNull();
    }

    [Fact]
    public void Select_IsAmbiguousWhenTheTitleMatchesInSeveralSeasonsOfTheSet()
    {
        // Arrange
        SeasonConstraint sut = StrictTo(1, 2);

        // Act & Assert
        sut.Select([Candidate(101, 1), Candidate(201, 2)]).ShouldBeNull();
    }

    [Fact]
    public void Judge_RejectsALinkOutsideTheSet()
    {
        // Arrange
        SeasonConstraint sut = StrictTo(1, 2);

        // Act & Assert
        sut.Judge(Link(301, 3), [Candidate(301, 3)]).ShouldBe(MatchVerdict.Rejected);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Judge_KeepsALinkInAnySeasonOfTheSet(int season)
    {
        // Arrange
        SeasonConstraint sut = StrictTo(1, 2);

        // Act & Assert
        sut.Judge(Link(season * 100, season), []).ShouldBe(MatchVerdict.Kept);
    }

    [Fact]
    public void EqualsAnotherConstraintOverTheSameSet()
    {
        // Act & Assert
        StrictTo(2, 1).ShouldBe(StrictTo(1, 2));
    }
}
