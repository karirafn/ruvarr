using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class CompleteMatchRecheck
{
    [Fact]
    public void ClearsTheMatchRecheckRequest()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithMultipleEpisodes().Build();
        sut.MatchTvdb(new TvdbSeriesBuilder().Build());
        sut.RequestRefresh();

        // Act
        sut.CompleteMatchRecheck();

        // Assert
        sut.MatchRecheckRequested.ShouldBeFalse();
    }
}
