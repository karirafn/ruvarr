using System.Security.Cryptography;

using Ruvarr.Programs.Domain;

namespace Ruvarr.UnitTests.Builders;

internal sealed class TmdbMovieBuilder
{
    private int _id = RandomNumberGenerator.GetInt32(1, 10_000);
    private string _name = "Test Movie";

    public TmdbMovieBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public TmdbMovieBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public TmdbMovie Build() => TmdbMovie.Create(
        id: _id,
        name: _name);
}
