using System.Security.Cryptography;

using Ruvarr.Ruv.Domain;

namespace Ruvarr.UnitTests.Builders;

internal sealed class RuvProgramBuilder
{
    private int _ruvId = RandomNumberGenerator.GetInt32(1, 10_000);
    private string _channel = "channel";
    private string _name = "name";
    private string? _foreignName = "fleh";
    private bool _hasMultipleEpisodes;

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

    public RuvProgram Build() => RuvProgram.Create(
        id: _ruvId,
        channel: _channel,
        name: _name,
        foreignName: _foreignName,
        multipleEpisodes: _hasMultipleEpisodes);
}