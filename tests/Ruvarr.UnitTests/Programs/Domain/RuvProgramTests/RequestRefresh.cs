using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class RequestRefresh
{
    [Fact]
    public void RaisesProgramRefreshRequestedEvent()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithRuvId(42).WithName("Test Program").Build();
        sut.ClearDomainEvents();

        // Act
        sut.RequestRefresh();

        // Assert
        ProgramRefreshRequestedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProgramRefreshRequestedEvent>();
        @event.RuvId.ShouldBe(42);
        @event.Name.ShouldBe("Test Program");
    }

    [Fact]
    public void SetsHasSeriesToTrueWhenProgramHasSeries()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.MatchTvdb(new TvdbSeriesBuilder().Build());
        sut.ClearDomainEvents();

        // Act
        sut.RequestRefresh();

        // Assert
        ProgramRefreshRequestedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProgramRefreshRequestedEvent>();
        @event.HasSeries.ShouldBeTrue();
    }

    [Fact]
    public void SetsHasSeriesToFalseWhenProgramHasNoSeries()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.ClearDomainEvents();

        // Act
        sut.RequestRefresh();

        // Assert
        ProgramRefreshRequestedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProgramRefreshRequestedEvent>();
        @event.HasSeries.ShouldBeFalse();
    }

    [Fact]
    public void SetsHasMultipleEpisodesFromProgram()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithMultipleEpisodes().Build();
        sut.ClearDomainEvents();

        // Act
        sut.RequestRefresh();

        // Assert
        ProgramRefreshRequestedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProgramRefreshRequestedEvent>();
        @event.HasMultipleEpisodes.ShouldBeTrue();
    }
}
