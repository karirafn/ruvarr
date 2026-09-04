using Ruvarr.Downloads.Domain;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain;

public sealed class RetryScheduleTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 24)]
    [InlineData(5, 168)]
    public void ComputeNextRetry_ReturnsCorrectRung(int retryCount, int expectedHours)
    {
        // Arrange
        DateTime before = DateTime.UtcNow;

        // Act
        DateTime result = RetrySchedule.ComputeNextRetry(retryCount);

        // Assert
        result.ShouldBeInRange(
            before.AddHours(expectedHours),
            DateTime.UtcNow.AddHours(expectedHours));
    }

    [Fact]
    public void ComputeNextRetry_ReturnsSameAsMaxRung_ForCountBeyondMax()
    {
        // Arrange — any count > 4 falls into the _ arm (7 days)
        DateTime before = DateTime.UtcNow;

        // Act
        DateTime result = RetrySchedule.ComputeNextRetry(RetrySchedule.MaxRetries + 1);

        // Assert
        result.ShouldBeInRange(
            before.AddDays(7),
            DateTime.UtcNow.AddDays(7));
    }

    [Fact]
    public void MaxRetries_IsFive()
    {
        // Assert
        RetrySchedule.MaxRetries.ShouldBe(5);
    }
}
