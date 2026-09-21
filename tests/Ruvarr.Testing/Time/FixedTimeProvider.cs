namespace Ruvarr.Testing.Time;

public sealed class FixedTimeProvider(DateTimeOffset fixedTime) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => fixedTime;
}
