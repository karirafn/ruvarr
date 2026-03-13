using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class MarkComplete
{
    [Fact]
    public void RemovesItemFromQueue()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");

        // Act
        sut.MarkComplete(1);

        // Assert
        sut.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AllowsReEnqueueAfterComplete()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.MarkComplete(1);

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        sut.Items.ShouldHaveSingleItem();
    }
}
