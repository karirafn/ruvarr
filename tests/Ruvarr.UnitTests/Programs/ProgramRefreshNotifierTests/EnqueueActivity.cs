using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Testing.Time;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class EnqueueActivity
{
    [Fact]
    public void WhenRecordEnqueueBatchCalled_SetsLastEnqueuedCount()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);

        // Act
        sut.RecordEnqueueBatch(5);

        // Assert
        sut.LastEnqueuedCount.ShouldBe(5);
    }

    [Fact]
    public void WhenRecordEnqueueBatchCalled_SetsLastEnqueuedAt()
    {
        // Arrange
        DateTimeOffset before = DateTimeOffset.UtcNow;
        ProgramRefreshNotifier sut = new(TimeProvider.System);

        // Act
        sut.RecordEnqueueBatch(3);

        // Assert
        DateTimeOffset after = DateTimeOffset.UtcNow;
        sut.LastEnqueuedAt.ShouldNotBeNull();
        sut.LastEnqueuedAt.Value.ShouldBeInRange(before, after);
    }

    [Fact]
    public void WhenNeverCalled_LastEnqueuedAtIsNull()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);

        // Act
        // (no call)

        // Assert
        sut.LastEnqueuedAt.ShouldBeNull();
    }

    [Fact]
    public void WhenNeverCalled_LastEnqueuedCountIsNull()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);

        // Act
        // (no call)

        // Assert
        sut.LastEnqueuedCount.ShouldBeNull();
    }

    [Fact]
    public void WhenCountIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);

        // Act
        Action act = () => sut.RecordEnqueueBatch(-1);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void WhenCountIsZero_IsAccepted()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);

        // Act
        sut.RecordEnqueueBatch(0);

        // Assert
        sut.LastEnqueuedCount.ShouldBe(0);
    }

    [Fact]
    public void WhenCalledWithTimeProvider_SetsLastEnqueuedAtFromProvider()
    {
        // Arrange
        DateTimeOffset fixedTime = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        FixedTimeProvider timeProvider = new(fixedTime);
        ProgramRefreshNotifier sut = new(timeProvider);

        // Act
        sut.RecordEnqueueBatch(7);

        // Assert
        sut.LastEnqueuedAt.ShouldBe(fixedTime);
    }

    [Fact]
    public void WhenConstructedWithTimeProvider_StartedAtIsFromProvider()
    {
        // Arrange
        DateTimeOffset fixedTime = new(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);
        FixedTimeProvider timeProvider = new(fixedTime);

        // Act
        ProgramRefreshNotifier sut = new(timeProvider);

        // Assert
        sut.StartedAt.ShouldBe(fixedTime);
    }

    [Fact]
    public void WhenConstructedWithSystemTimeProvider_StartedAtIsApproximatelyNow()
    {
        // Arrange
        DateTimeOffset before = DateTimeOffset.UtcNow;

        // Act
        ProgramRefreshNotifier sut = new(TimeProvider.System);

        // Assert
        DateTimeOffset after = DateTimeOffset.UtcNow;
        sut.StartedAt.ShouldBeInRange(before, after);
    }

}
