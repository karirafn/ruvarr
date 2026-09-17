using Ruvarr.Abstractions;
using Ruvarr.ProgramRefreshQueue.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class Enqueue
{
    [Fact]
    public void AllowsLease()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();

        // Act
        sut.Enqueue(1, "Program");

        // Assert
        IQueueLease? lease = sut.TryLeaseNext();
        lease.ShouldNotBeNull();
    }

    [Fact]
    public void DeduplicatesById()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program");

        // Act
        sut.Enqueue(1, "Program");

        // Assert
        IQueueLease? first = sut.TryLeaseNext();
        first.ShouldNotBeNull();
        IQueueLease? second = sut.TryLeaseNext();
        second.ShouldBeNull();
    }

    [Fact]
    public void AllowsReEnqueueAfterLeaseDisposed()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program");
        IQueueLease lease = sut.TryLeaseNext().ShouldNotBeNull();
        lease.Dispose();

        // Act
        sut.Enqueue(1, "Program");

        // Assert
        IQueueLease? newLease = sut.TryLeaseNext();
        newLease.ShouldNotBeNull();
    }
}
