using System.Security.Cryptography;

using Ruvarr.Programs.Domain;

namespace Ruvarr.Testing.Builders;

internal sealed class RuvProgramBuilder
{
    private int _ruvId = RandomNumberGenerator.GetInt32(1, 10_000);
    private string _channel = "channel";
    private string _name = "name";
    private string? _foreignName = "fleh";
    private bool _hasMultipleEpisodes;
    private string? _slug;
    private Uri? _imageUrl;
    private string? _description;

    public RuvProgramBuilder WithRuvId(int ruvId)
    {
        _ruvId = ruvId;
        return this;
    }

    public RuvProgramBuilder WithChannel(string channel)
    {
        _channel = channel;
        return this;
    }

    public RuvProgramBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RuvProgramBuilder WithForeignName(string? foreignName)
    {
        _foreignName = foreignName;
        return this;
    }

    public RuvProgramBuilder WithMultipleEpisodes(bool hasMultipleEpisodes = true)
    {
        _hasMultipleEpisodes = hasMultipleEpisodes;
        return this;
    }

    public RuvProgramBuilder WithSlug(string? slug)
    {
        _slug = slug;
        return this;
    }

    public RuvProgramBuilder WithImageUrl(Uri? imageUrl)
    {
        _imageUrl = imageUrl;
        return this;
    }

    public RuvProgramBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public RuvProgram Build() => RuvProgram.Create(
        id: _ruvId,
        channel: _channel,
        name: _name,
        foreignName: _foreignName,
        multipleEpisodes: _hasMultipleEpisodes,
        slug: _slug,
        imageUrl: _imageUrl,
        description: _description);
}
