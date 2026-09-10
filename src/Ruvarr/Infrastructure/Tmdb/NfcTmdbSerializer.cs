using System.Text;

using TMDbLib.Utilities.Serializer;

namespace Ruvarr.Infrastructure.Tmdb;

internal sealed class NfcTmdbSerializer : ITMDbSerializer
{
    // 4 MB cap — TMDb JSON payloads are small; this prevents an oversized or malicious
    // response from exhausting memory via an unbounded ReadToEnd call.
    private const int MaxResponseBytes = 4 * 1024 * 1024;

    // NFC normalization can theoretically expand adversarial NFD input in memory beyond
    // the network-read cap (e.g. if future Unicode additions produce an NFC form whose
    // UTF-8 encoding is longer than the NFD input). This cap bounds that expansion
    // independently of the pre-normalization read cap.
    private const int MaxNormalizedBytes = 2 * MaxResponseBytes;

    public object? Deserialize(Stream source, Type type)
    {
        // The source stream is intentionally left open: TMDbLib owns its lifetime
        // and reads headers/trailing data from it after this method returns.
        byte[] bodyBytes = ReadBounded(source);

        // Decode the bounded bytes to a string, respecting BOM detection so that a
        // TMDb response with a UTF-8 BOM is handled correctly.
        using StreamReader reader = new(
            new MemoryStream(bodyBytes),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: -1,
            leaveOpen: false);
        string rawJson = reader.ReadToEnd();

        // Whole-body NFC normalization is safe: NFC only composes base+combining
        // characters into precomposed code points (all >= U+00C0), so it can never
        // produce an ASCII JSON structural character (" \ { } [ ] , :). Structural
        // corruption is therefore impossible. This mirrors NfcStringConverter, which
        // applies the same normalization at the TMDb JSON boundary.
        string normalizedJson = rawJson.IsNormalized(NormalizationForm.FormC)
            ? rawJson
            : rawJson.Normalize(NormalizationForm.FormC);

        // GetBytes never prepends a BOM (preamble is a StreamWriter concern), so the
        // re-wrapped bytes are always BOM-free, as TMDbJsonSerializer.Instance expects.
        byte[] normalizedBytes = Encoding.UTF8.GetBytes(normalizedJson);

        // NFC normalization can expand adversarial NFD payloads in memory even when the
        // original network read passed the pre-normalization cap. Bound the post-normalization
        // byte length independently to prevent unbounded allocation after the stream is read.
        if (normalizedBytes.Length > MaxNormalizedBytes)
        {
            throw new InvalidOperationException(
                $"Normalized TMDb response body exceeded {MaxNormalizedBytes} bytes.");
        }

        using MemoryStream normalizedStream = new(normalizedBytes);

        return TMDbJsonSerializer.Instance.Deserialize(normalizedStream, type);
    }

    public void Serialize(Stream target, object obj, Type type) =>
        TMDbJsonSerializer.Instance.Serialize(target, obj, type);

    private static byte[] ReadBounded(Stream source)
    {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[81920];
        int totalRead = 0;
        int bytesRead;

        while ((bytesRead = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > MaxResponseBytes)
            {
                throw new InvalidOperationException(
                    $"TMDb response body exceeded {MaxResponseBytes} bytes.");
            }

            buffer.Write(chunk, 0, bytesRead);
        }

        return buffer.ToArray();
    }
}
