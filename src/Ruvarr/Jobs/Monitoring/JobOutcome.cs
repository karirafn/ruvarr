namespace Ruvarr.Jobs.Monitoring;

internal sealed record JobOutcome(
    int ConsecutiveFailures,
    DateTimeOffset? LastFailureAt,
    DateTimeOffset? LastSuccessAt,
    string? LastError);
