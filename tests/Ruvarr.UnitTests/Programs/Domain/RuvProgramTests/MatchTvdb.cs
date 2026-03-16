using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class MatchTvdb
{
    [Fact]
    public void SetsSeries()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        TvdbSeries series = new TvdbSeriesBuilder().Build();

        // Act
        sut.MatchTvdb(series);

        // Assert
        sut.Series.ShouldBe(series);
    }

    [Fact]
    public void SetsMatchedToUtcNow()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        DateTime before = DateTime.UtcNow;

        // Act
        sut.MatchTvdb(new TvdbSeriesBuilder().Build());

        // Assert
        sut.Matched.ShouldNotBeNull();
        sut.Matched.Value.ShouldBeInRange(before, DateTime.UtcNow);
    }

    [Fact]
    public void ClearsNextLookup()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.ScheduleLookup();
        sut.NextLookup.ShouldNotBeNull();

        // Act
        sut.MatchTvdb(new TvdbSeriesBuilder().Build());

        // Assert
        sut.NextLookup.ShouldBeNull();
    }

    [Fact]
    public void IncrementsLookupCount()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();

        // Act
        sut.MatchTvdb(new TvdbSeriesBuilder().Build());

        // Assert
        sut.LookupCount.ShouldBe(1);
    }

    [Fact]
    public void RaisesProgramMatchedTvdbEvent()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithRuvId(42).WithName("Test Program").Build();
        sut.ClearDomainEvents();

        // Act
        sut.MatchTvdb(new TvdbSeriesBuilder().Build());

        // Assert
        ProgramMatchedTvdbEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProgramMatchedTvdbEvent>();
        @event.RuvId.ShouldBe(42);
        @event.Name.ShouldBe("Test Program");
    }
}
