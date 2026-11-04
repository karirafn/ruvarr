using System.Text;

using Ruvarr.Infrastructure.Tmdb;

using Shouldly;

using TMDbLib.Objects.Movies;

namespace Ruvarr.UnitTests.Infrastructure.Tmdb.NfcTmdbSerializerTests;

public sealed class Serialize
{
    private readonly NfcTmdbSerializer _sut = new();

    [Fact]
    public void WhenSerializing_WritesValueUnchangedWithoutNormalizing()
    {
        // Arrange -- NFD form: "e" + U+0301 (COMBINING ACUTE ACCENT).
        // \u escapes guarantee no raw combining bytes appear in source.
        // Serialize must emit the string unchanged; normalization is Deserialize's job, not Serialize's.
        string nfdValue = "e\u0301";
        using MemoryStream target = new();

        // Act
        _sut.Serialize(target, new Movie { Title = nfdValue }, typeof(Movie));

        // Assert -- TMDbLib's JSON encoder escapes non-ASCII as \uXXXX, so the output keeps the
        // NFD pair "e" + \u0301 and never the NFC precomposed \u00e9, proving Serialize did not normalize.
        string output = Encoding.UTF8.GetString(target.ToArray());
        output.ShouldContain("\"title\":\"e\\u0301\"");
    }
}
