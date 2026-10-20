using Ruvarr.Infrastructure.Ruv.Models;

namespace Ruvarr.Testing.Builders;

internal sealed class RuvTvEpisodeBuilder
{
    private string _id = Guid.NewGuid().ToString()[..8];
    private string? _title = "Test Episode";
    private IReadOnlyList<string> _description = ["Test description"];
    private DateTime _firstRun = DateTime.UtcNow;
    private int _duration = 1800;
    private Uri _file = new("https://ruv.is/stream.m3u8");

    public RuvTvEpisodeBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public RuvTvEpisodeBuilder WithTitle(string? title)
    {
        _title = title;
        return this;
    }

    public RuvTvEpisodeBuilder WithDescription(IReadOnlyList<string> description)
    {
        _description = description;
        return this;
    }

    public RuvTvEpisodeBuilder WithFirstRun(DateTime firstRun)
    {
        _firstRun = firstRun;
        return this;
    }

    public RuvTvEpisodeBuilder WithDuration(int durationSeconds)
    {
        _duration = durationSeconds;
        return this;
    }

    public RuvTvEpisodeBuilder WithFile(Uri file)
    {
        _file = file;
        return this;
    }

    public RuvTvEpisode Build() => new(
        Id: _id,
        Number: 1,
        SeriesId: 0,
        FirstRun: _firstRun,
        FileExpires: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        Rating: 0,
        Duration: _duration,
        DurationFriendly: "30 min",
        Event: 0,
        Title: _title,
        Slug: "test-episode",
        Description: _description,
        ImageRenditions: new RuvImageRenditions([]),
        Image: new Uri("https://ruv.is/image.jpg"),
        ImageOg: new Uri("https://ruv.is/image-og.jpg"),
        Scope: "ruv",
        SubtitlesUrl: new Uri("https://ruv.is/subs.vtt"),
        Subtitles: new RuvSubtitles(new Uri("https://ruv.is/subs-is.vtt")),
        OpenSubtitles: false,
        ClosedSubtitles: false,
        AutoSubtitles: false,
        File: _file,
        Temp: new RuvTemp("file.mp4", "folder"),
        Clips: [],
        Files: new RuvFiles(new RuvVodmp4("file.mp4", "folder", "file", false, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), 0, "0", "mp4", "ruv")),
        CreditPoint: 0);
}
