using System.Globalization;
using System.Security.Cryptography;

using Ruvarr.Programs.Domain;

namespace Ruvarr.UnitTests.Builders;

internal sealed class TvdbSeriesBuilder
{
    private string _id = RandomNumberGenerator.GetInt32(1, 10000).ToString(CultureInfo.InvariantCulture);
    private string _type = "series";
    private string _name = "Test series";

    public TvdbSeriesBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public TvdbSeriesBuilder WithType(string type)
    {
        _type = type;
        return this;
    }

    public TvdbSeriesBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public TvdbSeries Build() => TvdbSeries.Create(
        id: _id,
        type: _type,
        name: _name);
}