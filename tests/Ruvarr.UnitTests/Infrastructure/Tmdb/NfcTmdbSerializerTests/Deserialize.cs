using System.Text;

using Ruvarr.Infrastructure.Tmdb;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tmdb.NfcTmdbSerializerTests;

public sealed class Deserialize
{
    // NFD: o + U+0308 (COMBINING DIAERESIS), a + U+0301 (COMBINING ACUTE ACCENT)
    // Written with explicit \u escapes so git/editor normalization cannot collapse them.
    private const string NfdTitle = "Skjaldbo\u006f\u0308kustra\u0061\u0301kur";
    private const string NfcTitle = "Skjaldbo\u00f6kustra\u00e1kur";

    private readonly NfcTmdbSerializer _sut = new();

    [Fact]
    public void WhenJsonContainsNfdStrings_DeserializesToNfc()
    {
        // Arrange
        string json = $"{{\"Title\":\"{NfdTitle}\",\"Id\":42}}";
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));

        // Act
        object? result = _sut.Deserialize(stream, typeof(TestMovie));

        // Assert
        TestMovie movie = result.ShouldBeOfType<TestMovie>();
        movie.Title.ShouldBe(NfcTitle);
        movie.Title.IsNormalized(NormalizationForm.FormC).ShouldBeTrue();
    }

    [Fact]
    public void WhenJsonContainsNfcStrings_PassesThroughUnchanged()
    {
        // Arrange
        string json = $"{{\"Title\":\"{NfcTitle}\",\"Id\":42}}";
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));

        // Act
        object? result = _sut.Deserialize(stream, typeof(TestMovie));

        // Assert
        TestMovie movie = result.ShouldBeOfType<TestMovie>();
        movie.Title.ShouldBe(NfcTitle);
        movie.Title.IsNormalized(NormalizationForm.FormC).ShouldBeTrue();
    }

    [Fact]
    public void WhenJsonContainsAsciiStrings_PassesThroughUnchanged()
    {
        // Arrange
        string json = "{\"Title\":\"Inception\",\"Id\":1}";
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));

        // Act
        object? result = _sut.Deserialize(stream, typeof(TestMovie));

        // Assert
        TestMovie movie = result.ShouldBeOfType<TestMovie>();
        movie.Title.ShouldBe("Inception");
    }

    private sealed class TestMovie
    {
        public string Title { get; set; } = string.Empty;
    }
}
