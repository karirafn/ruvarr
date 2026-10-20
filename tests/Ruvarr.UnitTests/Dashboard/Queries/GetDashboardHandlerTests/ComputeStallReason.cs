using Ruvarr.Dashboard;
using Ruvarr.Dashboard.Queries.GetDashboard;
using Ruvarr.Jobs.Monitoring;

using Shouldly;

namespace Ruvarr.UnitTests.Dashboard.Queries.GetDashboardHandlerTests;

public sealed class ComputeStallReason
{
    private static readonly DateTimeOffset FixedTime = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WhenStalledAndProgramRefreshFailing_ReturnsUpstreamRefreshFailing()
    {
        // Arrange
        TimeSpan stalledFor = TimeSpan.FromHours(3);
        JobOutcome programRefreshOutcome = new(
            ConsecutiveFailures: 2,
            LastFailureAt: FixedTime,
            LastSuccessAt: null,
            LastError: "test error");

        // Act
        StallReason result = GetDashboardHandler.ComputeStallReason(stalledFor, programRefreshOutcome);

        // Assert
        result.ShouldBe(StallReason.UpstreamRefreshFailing);
    }

    [Fact]
    public void WhenStalledButNoRefreshFailures_ReturnsUnknown()
    {
        // Arrange
        TimeSpan stalledFor = TimeSpan.FromHours(3);
        JobOutcome programRefreshOutcome = new(
            ConsecutiveFailures: 0,
            LastFailureAt: FixedTime.AddHours(-4),
            LastSuccessAt: FixedTime.AddMinutes(-30),
            LastError: null);

        // Act
        StallReason result = GetDashboardHandler.ComputeStallReason(stalledFor, programRefreshOutcome);

        // Assert
        result.ShouldBe(StallReason.Unknown);
    }

    [Fact]
    public void WhenNotStalled_ReturnsUnknown()
    {
        // Arrange
        TimeSpan? stalledFor = null;
        JobOutcome programRefreshOutcome = new(
            ConsecutiveFailures: 5,
            LastFailureAt: FixedTime,
            LastSuccessAt: null,
            LastError: "error");

        // Act
        StallReason result = GetDashboardHandler.ComputeStallReason(stalledFor, programRefreshOutcome);

        // Assert
        result.ShouldBe(StallReason.Unknown);
    }

    [Fact]
    public void WhenStalledAndProgramRefreshOutcomeIsNull_ReturnsUnknown()
    {
        // Arrange
        TimeSpan stalledFor = TimeSpan.FromHours(3);

        // Act
        StallReason result = GetDashboardHandler.ComputeStallReason(stalledFor, null);

        // Assert
        result.ShouldBe(StallReason.Unknown);
    }
}
