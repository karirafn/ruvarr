using System.Security.Cryptography;

using Ruvarr.Programs.Domain;

namespace Ruvarr.UnitTests.Builders;

internal sealed class TvdbSeriesBuilder
{
    private int _id = RandomNumberGenerator.GetInt32(1, 10000);
    private string _name = "Test series";
    private string? _slug;

    public TvdbSeriesBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public TvdbSeriesBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public TvdbSeriesBuilder WithSlug(string? slug)
    {
        _slug = slug;
        return this;
    }

    public TvdbSeries Build() => TvdbSeries.Create(
        id: _id,
        name: _name,
        slug: _slug);
}