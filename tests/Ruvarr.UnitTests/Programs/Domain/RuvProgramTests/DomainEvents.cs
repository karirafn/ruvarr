using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class DomainEvents
{
    [Fact]
    public void Create_RaisesProgramCreatedEvent()
    {
        // Act
        RuvProgram sut = new RuvProgramBuilder().Build();

        // Assert
        ProgramCreatedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProgramCreatedEvent>();
        @event.Program.ShouldBe(sut);
    }

    [Fact]
    public void ClearDomainEvents_ClearsAllEvents()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();

        // Act
        sut.ClearDomainEvents();

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void SetMonitoredStatus_FromFalseToTrue_RaisesProgramMonitoredStatusChangedEvent()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.ClearDomainEvents();

        // Act
        sut.SetMonitoredStatus(true);

        // Assert
        ProgramMonitoredStatusChangedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProgramMonitoredStatusChangedEvent>();
        @event.RuvId.ShouldBe(sut.RuvId);
        @event.IsMonitored.ShouldBeTrue();
    }

    [Fact]
    public void SetMonitoredStatus_FromTrueToFalse_RaisesProgramMonitoredStatusChangedEvent()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.ClearDomainEvents();
        sut.SetMonitoredStatus(true);
        sut.ClearDomainEvents();

        // Act
        sut.SetMonitoredStatus(false);

        // Assert
        ProgramMonitoredStatusChangedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProgramMonitoredStatusChangedEvent>();
        @event.RuvId.ShouldBe(sut.RuvId);
        @event.IsMonitored.ShouldBeFalse();
    }

    [Fact]
    public void SetMonitoredStatus_SameValue_DoesNotRaiseEvent()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.ClearDomainEvents();

        // Act
        sut.SetMonitoredStatus(false);

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }
}
