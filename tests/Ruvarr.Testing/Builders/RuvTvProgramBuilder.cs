using System.Security.Cryptography;

using Ruvarr.Infrastructure.Ruv.Models;

namespace Ruvarr.Testing.Builders;

internal sealed class RuvTvProgramBuilder
{
    private int _id = RandomNumberGenerator.GetInt32(1, 10_000);
    private string _title = "Test Program";
    private string? _foreignTitle = "Test Program Foreign";
    private string? _slug = "test-program";
    private Uri? _image;
    private IReadOnlyList<string> _description = ["Test description"];
    private bool _multipleEpisodes = true;
    private int _webAvailableEpisodes = 1;
    private string _channel = "ruv";
    private IReadOnlyList<RuvTvEpisode> _episodes = [];

    public RuvTvProgramBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public RuvTvProgramBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public RuvTvProgramBuilder WithForeignTitle(string? foreignTitle)
    {
        _foreignTitle = foreignTitle;
        return this;
    }

    public RuvTvProgramBuilder WithSlug(string? slug)
    {
        _slug = slug;
        return this;
    }

    public RuvTvProgramBuilder WithImage(Uri? image)
    {
        _image = image;
        return this;
    }

    public RuvTvProgramBuilder WithDescription(IReadOnlyList<string> description)
    {
        _description = description;
        return this;
    }

    public RuvTvProgramBuilder WithMultipleEpisodes(bool multipleEpisodes = true)
    {
        _multipleEpisodes = multipleEpisodes;
        return this;
    }

    public RuvTvProgramBuilder WithWebAvailableEpisodes(int webAvailableEpisodes)
    {
        _webAvailableEpisodes = webAvailableEpisodes;
        return this;
    }

    public RuvTvProgramBuilder WithChannel(string channel)
    {
        _channel = channel;
        return this;
    }

    public RuvTvProgramBuilder WithEpisodes(IReadOnlyList<RuvTvEpisode> episodes)
    {
        _episodes = episodes;
        return this;
    }

    public RuvTvProgram Build() => new(
        Id: _id,
        Title: _title,
        ForeignTitle: _foreignTitle,
        Slug: _slug,
        Image: _image,
        Description: _description,
        MultipleEpisodes: _multipleEpisodes,
        Episodes: _episodes,
        WebAvailableEpisodes: _webAvailableEpisodes,
        Channel: _channel);
}
