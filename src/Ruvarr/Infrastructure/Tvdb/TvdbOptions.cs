namespace Ruvarr.Infrastructure.Tvdb;

internal sealed class TvdbOptions
{
    internal const string SectionName = "Tvdb";
    internal const string AccessTokenCacheKey = "TvdbAccessToken";

    public required string ApiKey { get; init; }

    public required Uri BaseAddress { get; init; }
}