namespace Ruvarr.Infrastructure.Ruv;

internal sealed class RuvStreamInspector(HttpClient httpClient) : IRuvStreamInspector
{
    private const string MasterPlaylistMarker = "#EXT-X-STREAM-INF";

    public async Task<long?> EstimateStreamSizeAsync(Uri m3u8Uri, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage getResponse = await httpClient.GetAsync(m3u8Uri, cancellationToken);
            if (!getResponse.IsSuccessStatusCode)
            {
                return null;
            }

            string content = await getResponse.Content.ReadAsStringAsync(cancellationToken);
            string[] lines = content.Split('\n');

            Uri mediaPlaylistUri = m3u8Uri;
            string mediaContent;

            if (lines.Any(line => line.TrimStart().StartsWith(MasterPlaylistMarker, StringComparison.Ordinal)))
            {
                string? variantUrl = ExtractLastVariantUrl(lines);
                if (variantUrl is null)
                {
                    return null;
                }

                Uri variantUri = new(m3u8Uri, variantUrl);
                if (variantUri.Host != m3u8Uri.Host || variantUri.Scheme != "https")
                {
                    return null;
                }

                using HttpResponseMessage variantResponse = await httpClient.GetAsync(variantUri, cancellationToken);
                if (!variantResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                mediaContent = await variantResponse.Content.ReadAsStringAsync(cancellationToken);
                mediaPlaylistUri = variantUri;
            }
            else
            {
                mediaContent = content;
            }

            string[] mediaLines = mediaContent.Split('\n');

            List<string> segments = mediaLines
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToList();

            if (segments.Count == 0)
            {
                return null;
            }

            Uri firstSegmentUri = new(mediaPlaylistUri, segments[0]);

            if (firstSegmentUri.Host != m3u8Uri.Host || firstSegmentUri.Scheme != "https")
            {
                return null;
            }

            using HttpRequestMessage headRequest = new(HttpMethod.Head, firstSegmentUri);
            using HttpResponseMessage headResponse = await httpClient.SendAsync(headRequest, cancellationToken);

            long? contentLength = headResponse.Content.Headers.ContentLength;
            if (contentLength is null or <= 0)
            {
                return null;
            }

            return segments.Count * contentLength.Value;
        }
#pragma warning disable CA1031 // Return null on any failure
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    private static string? ExtractLastVariantUrl(string[] lines)
    {
        string? lastVariantUrl = null;

        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (!lines[i].TrimStart().StartsWith(MasterPlaylistMarker, StringComparison.Ordinal))
            {
                continue;
            }

            string candidate = lines[i + 1].Trim();
            if (candidate.Length > 0 && !candidate.StartsWith('#'))
            {
                lastVariantUrl = candidate;
            }
        }

        return lastVariantUrl;
    }
}
