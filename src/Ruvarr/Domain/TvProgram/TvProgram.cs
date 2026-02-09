namespace Ruvarr.Domain.TvProgram;

internal sealed class TvProgram
{
    private TvProgram()
    {
    }

    public required int RuvId { get; init; }

    public required string RuvChannel { get; init; }

    public required string RuvName { get; init; }

    public required string? RuvForeignName { get; init; }

    public required bool HasMultipleEpisodes { get; init; }

    public required DateTime Created { get; init; }

    public DateTime? Matched { get; private set; }

    public DateTime? NextLookup { get; private set; }

    public int LookupCount { get; private set; }

    public string? TvdbId { get; private set; }

    public string? TvdbType { get; private set; }

    public string? TvdbName { get; private set; }

    public static TvProgram Create(int id, string channgel, string name, string? foreignName, bool multipleEpisodes) => new()
    {
        RuvId = id,
        RuvChannel = channgel,
        RuvName = name,
        RuvForeignName = foreignName,
        HasMultipleEpisodes = multipleEpisodes,
        Created = DateTime.UtcNow,
    };

    public void MatchTvdb(string id, string type, string name)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        LookupCount++;
        NextLookup = null;
        Matched = DateTime.UtcNow;
        TvdbId = id;
        TvdbType = type;
        TvdbName = name;
    }

    public void ScheduleLookup()
    {
        LookupCount++;

        DateTime now = DateTime.UtcNow;
        NextLookup = LookupCount switch
        {
            1 => now.AddHours(1),
            2 => now.AddHours(2),
            3 => now.AddHours(4),
            4 => now.AddDays(1),
            _ => now.AddDays(7)
        };
    }
}