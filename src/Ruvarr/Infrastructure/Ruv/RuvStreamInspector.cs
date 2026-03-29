using System.Globalization;
using System.Text.RegularExpressions;

namespace Ruvarr.Infrastructure.Ruv;

internal sealed partial class RuvStreamInspector(HttpClient httpClient) : IRuvStreamInspector
{
    private const string MasterPlaylistMarker = "#EXT-X-STREAM-INF";
    private const string ExtInfMarker = "#EXTINF:";
    public async Task<StreamSizeEstimate?> EstimateStreamSizeAsync(Uri m3u8Uri, CancellationToken cancellationToken)
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

            long? estimatedBytes = await EstimateTotalBytesAsync(
                segments, mediaPlaylistUri, m3u8Uri, cancellationToken);

            if (estimatedBytes is null)
            {
                return null;
            }

            double totalDurationSeconds = ParseTotalDuration(mediaLines);

            return new StreamSizeEstimate(estimatedBytes.Value, totalDurationSeconds);
        }
#pragma warning disable CA1031 // Return null on any failure
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    private async Task<long?> EstimateTotalBytesAsync(
        List<string> segments, Uri mediaPlaylistUri, Uri m3u8Uri, CancellationToken cancellationToken)
    {
        long? firstSize = await GetSegmentSizeAsync(segments[0], mediaPlaylistUri, m3u8Uri, cancellationToken);
        if (firstSize is null)
        {
            return null;
        }

        if (segments.Count == 1)
        {
            return firstSize.Value;
        }

        long? lastSize = await GetSegmentSizeAsync(segments[^1], mediaPlaylistUri, m3u8Uri, cancellationToken);
        if (lastSize is null)
        {
            return null;
        }

        return (firstSize.Value * (segments.Count - 1)) + lastSize.Value;
    }

    private async Task<long?> GetSegmentSizeAsync(
        string segment, Uri mediaPlaylistUri, Uri m3u8Uri, CancellationToken cancellationToken)
    {
        Uri segmentUri = new(mediaPlaylistUri, segment);

        if (segmentUri.Host != m3u8Uri.Host || segmentUri.Scheme != "https")
        {
            return null;
        }

        using HttpRequestMessage headRequest = new(HttpMethod.Head, segmentUri);
        using HttpResponseMessage headResponse = await httpClient.SendAsync(headRequest, cancellationToken);

        long? contentLength = headResponse.Content.Headers.ContentLength;
        return contentLength is > 0 ? contentLength.Value : null;
    }

    private static double ParseTotalDuration(string[] mediaLines)
    {
        double total = 0;

        foreach (string line in mediaLines)
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith(ExtInfMarker, StringComparison.Ordinal))
            {
                continue;
            }

            string durationPart = trimmed[ExtInfMarker.Length..];
            int commaIndex = durationPart.IndexOf(',', StringComparison.Ordinal);
            if (commaIndex >= 0)
            {
                durationPart = durationPart[..commaIndex];
            }

            if (double.TryParse(durationPart, CultureInfo.InvariantCulture, out double duration))
            {
                total += duration;
            }
        }

        return total;
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
