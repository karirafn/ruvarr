namespace Ruvarr.Jobs.Monitoring;

public sealed record JobOutcome(
    int ConsecutiveFailures,
    DateTimeOffset? LastFailureAt,
    DateTimeOffset? LastSuccessAt,
    string? LastError);
