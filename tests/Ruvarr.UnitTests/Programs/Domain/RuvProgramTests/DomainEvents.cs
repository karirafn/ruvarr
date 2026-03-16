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
}
