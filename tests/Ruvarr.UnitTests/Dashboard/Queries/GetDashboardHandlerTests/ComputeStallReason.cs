using Ruvarr.Dashboard;
using Ruvarr.Dashboard.Queries.GetDashboard;
using Ruvarr.Jobs.Monitoring;

using Shouldly;

namespace Ruvarr.UnitTests.Dashboard.Queries.GetDashboardHandlerTests;

public sealed class ComputeStallReason
{
    [Fact]
    public void WhenStalledAndProgramRefreshFailing_ReturnsUpstreamRefreshFailing()
    {
        // Arrange
        TimeSpan stalledFor = TimeSpan.FromHours(3);
        JobOutcome programRefreshOutcome = new(
            ConsecutiveFailures: 2,
            LastFailureAt: DateTimeOffset.UtcNow,
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
            LastFailureAt: DateTimeOffset.UtcNow.AddHours(-4),
            LastSuccessAt: DateTimeOffset.UtcNow.AddMinutes(-30),
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
            LastFailureAt: DateTimeOffset.UtcNow,
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
