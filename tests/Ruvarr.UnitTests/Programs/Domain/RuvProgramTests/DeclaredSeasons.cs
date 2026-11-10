using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class DeclaredSeasons
{
    [Fact]
    public void AreTheManualSeasonsWhenSet()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Bursti I").Build();
        sut.MatchTvdb(new TvdbSeriesBuilder().Build());
        sut.SetManualSeasons([2, 1], [1, 2, 3]);

        // Act & Assert
        sut.DeclaredSeasons.ShouldBe([1, 2]);
    }

    [Fact]
    public void AreTheSeasonParsedFromTheNameOtherwise()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Bursti III").Build();

        // Act & Assert
        sut.DeclaredSeasons.ShouldBe([3]);
    }

    [Fact]
    public void AreEmptyWhenNothingIsDeclared()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Hundurinn Ibbi").WithForeignName(null).Build();

        // Act & Assert
        sut.DeclaredSeasons.ShouldBeEmpty();
    }
}
