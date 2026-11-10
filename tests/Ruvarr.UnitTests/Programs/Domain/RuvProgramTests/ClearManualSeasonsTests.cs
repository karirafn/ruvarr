using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class ClearManualSeasonsTests
{
    [Fact]
    public void DeclaredSeasonsReturnToTheSeasonParsedFromTheName()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Ráðherrann II").Build();
        sut.MatchTvdb(new TvdbSeriesBuilder().Build());
        sut.SetManualSeasons([1, 3], [1, 2, 3]);

        // Act
        sut.ClearManualSeasons();

        // Assert
        sut.ManualSeasons.ShouldBeNull();
        sut.DeclaredSeasons.ShouldBe([2]);
    }
}
