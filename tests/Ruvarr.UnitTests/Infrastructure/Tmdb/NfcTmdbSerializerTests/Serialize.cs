using System.Text;

using Ruvarr.Infrastructure.Tmdb;

using Shouldly;

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
        _sut.Serialize(target, nfdValue, typeof(string));

        // Assert -- the output bytes contain the raw UTF-8 NFD sequence for
        // e (0x65) + COMBINING ACUTE ACCENT (0xCC 0x81), not the NFC precomposed
        // U+00E9 (0xC3 0xA9), proving Serialize did not normalize.
        byte[] bytes = target.ToArray();
        bytes.ShouldContain((byte)0x65); // e
        bytes.ShouldContain((byte)0xCC); // first byte of U+0301 in UTF-8
        bytes.ShouldContain((byte)0x81); // second byte of U+0301 in UTF-8
        // 0xC3 0xA9 is NFC U+00E9 (é); if these appeared back-to-back the output was normalized
        string output = Encoding.UTF8.GetString(bytes);
        output.IsNormalized(NormalizationForm.FormC).ShouldBeFalse();
    }
}
