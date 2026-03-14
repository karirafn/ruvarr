using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class SetMonitoredStatus
{
    [Fact]
    public void SetMonitoredStatus_SetsIsMonitoredToTrue()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();

        // Act
        sut.SetMonitoredStatus(true);

        // Assert
        sut.IsMonitored.ShouldBeTrue();
    }

    [Fact]
    public void SetMonitoredStatus_SetsIsMonitoredToFalse()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.SetMonitoredStatus(true);

        // Act
        sut.SetMonitoredStatus(false);

        // Assert
        sut.IsMonitored.ShouldBeFalse();
    }
}
