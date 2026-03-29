using System.Text.RegularExpressions;

namespace Ruvarr.Infrastructure.Ruv;

internal sealed partial class RuvStreamInspector(HttpClient httpClient) : IRuvStreamInspector
{
    private const string MasterPlaylistMarker = "#EXT-X-STREAM-INF";
    private const int MaxSegmentsToSample = 3;

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
                string? variantUrl = ExtractHighestBandwidthVariantUrl(lines);
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

            long? averageSegmentSize = await GetAverageSegmentSizeAsync(
                segments, mediaPlaylistUri, m3u8Uri, cancellationToken);

            if (averageSegmentSize is null)
            {
                return null;
            }

            return segments.Count * averageSegmentSize.Value;
        }
#pragma warning disable CA1031 // Return null on any failure
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    private async Task<long?> GetAverageSegmentSizeAsync(
        List<string> segments, Uri mediaPlaylistUri, Uri m3u8Uri, CancellationToken cancellationToken)
    {
        int samplesToTake = Math.Min(segments.Count, MaxSegmentsToSample);
        List<long> sizes = [];

        for (int i = 0; i < samplesToTake; i++)
        {
            Uri segmentUri = new(mediaPlaylistUri, segments[i]);

            if (segmentUri.Host != m3u8Uri.Host || segmentUri.Scheme != "https")
            {
                return null;
            }

            using HttpRequestMessage headRequest = new(HttpMethod.Head, segmentUri);
            using HttpResponseMessage headResponse = await httpClient.SendAsync(headRequest, cancellationToken);

            long? contentLength = headResponse.Content.Headers.ContentLength;
            if (contentLength is > 0)
            {
                sizes.Add(contentLength.Value);
            }
        }

        if (sizes.Count == 0)
        {
            return null;
        }

        return (long)sizes.Average();
    }

    private static string? ExtractHighestBandwidthVariantUrl(string[] lines)
    {
        string? bestVariantUrl = null;
        long highestBandwidth = -1;
        bool hasBandwidth = false;

        for (int i = 0; i < lines.Length - 1; i++)
        {
            string trimmedLine = lines[i].TrimStart();
            if (!trimmedLine.StartsWith(MasterPlaylistMarker, StringComparison.Ordinal))
            {
                continue;
            }

            string candidate = lines[i + 1].Trim();
            if (candidate.Length == 0 || candidate.StartsWith('#'))
            {
                continue;
            }

            Match match = BandwidthPattern().Match(trimmedLine);
            if (match.Success && long.TryParse(match.Groups[1].Value, out long bandwidth))
            {
                hasBandwidth = true;
                if (bandwidth > highestBandwidth)
                {
                    highestBandwidth = bandwidth;
                    bestVariantUrl = candidate;
                }
            }
            else if (!hasBandwidth)
            {
                bestVariantUrl = candidate;
            }
        }

        return bestVariantUrl;
    }

    [GeneratedRegex(@"BANDWIDTH=(\d+)")]
    private static partial Regex BandwidthPattern();
}
