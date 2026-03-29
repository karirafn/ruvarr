namespace Ruvarr.Infrastructure.Ruv;

internal sealed class RuvStreamInspector(HttpClient httpClient) : IRuvStreamInspector
{
    private const string MasterPlaylistMarker = "#EXT-X-STREAM-INF";

    public async Task<long?> EstimateStreamSizeAsync(Uri m3u8Uri, CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage getResponse = await httpClient.GetAsync(m3u8Uri, cancellationToken);
            if (!getResponse.IsSuccessStatusCode)
            {
                return null;
            }

            string content = await getResponse.Content.ReadAsStringAsync(cancellationToken);
            string[] lines = content.Split('\n');

            if (lines.Any(line => line.TrimStart().StartsWith(MasterPlaylistMarker, StringComparison.Ordinal)))
            {
                return null;
            }

            List<string> segments = lines
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToList();

            if (segments.Count == 0)
            {
                return null;
            }

            Uri firstSegmentUri = new(m3u8Uri, segments[0]);

            using HttpRequestMessage headRequest = new(HttpMethod.Head, firstSegmentUri);
            HttpResponseMessage headResponse = await httpClient.SendAsync(headRequest, cancellationToken);

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
}
