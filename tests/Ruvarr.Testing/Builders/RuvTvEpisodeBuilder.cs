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
        FirstRun: _firstRun,
        Duration: _duration,
        Title: _title,
        Description: _description,
        File: _file);
}
