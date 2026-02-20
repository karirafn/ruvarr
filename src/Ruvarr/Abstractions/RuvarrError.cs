namespace Ruvarr.Abstractions;

public sealed record class RuvarrError(string Code, string Description)
{
    public static readonly RuvarrError None = new(string.Empty, string.Empty);

    public static implicit operator RuvarrResult(RuvarrError error) => RuvarrResult.Failure(error);

    public RuvarrResult ToRuvarrResult() => this;
}