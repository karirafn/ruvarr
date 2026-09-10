using System.Text;

using TMDbLib.Utilities.Serializer;

namespace Ruvarr.Infrastructure.Tmdb;

internal sealed class NfcTmdbSerializer : ITMDbSerializer
{
    public object? Deserialize(Stream source, Type type)
    {
        using StreamReader reader = new(source, Encoding.UTF8, leaveOpen: true);
        string rawJson = reader.ReadToEnd();

        string normalizedJson = rawJson.IsNormalized(NormalizationForm.FormC)
            ? rawJson
            : rawJson.Normalize(NormalizationForm.FormC);

        byte[] normalizedBytes = Encoding.UTF8.GetBytes(normalizedJson);
        using MemoryStream normalizedStream = new(normalizedBytes);

        return TMDbJsonSerializer.Instance.Deserialize(normalizedStream, type);
    }

    public void Serialize(Stream target, object obj, Type type) =>
        TMDbJsonSerializer.Instance.Serialize(target, obj, type);
}
