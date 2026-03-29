using System.Globalization;
using System.Text.RegularExpressions;

namespace Ruvarr.Infrastructure.FFmpeg;

internal static partial class FfmpegStderrParser
{
    [GeneratedRegex(@"size=\s*(\d+)\s*kB", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SizePattern();

    [GeneratedRegex(@"time=\s*(\d{2}):(\d{2}):(\d{2})\.(\d{2})", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TimePattern();

    [GeneratedRegex(@"Duration:\s*(\d{2}):(\d{2}):(\d{2})\.(\d{2}).*bitrate:\s*(\d+)\s*kb/s", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DurationPattern();

    public static long? TryParseDuration(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        Match match = DurationPattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes) ||
            !int.TryParse(match.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) ||
            !int.TryParse(match.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int centiseconds) ||
            !long.TryParse(match.Groups[5].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long bitrateKbps))
        {
            return null;
        }

        double durationSeconds = (hours * 3600) + (minutes * 60) + seconds + (centiseconds / 100.0);

        long estimated = (long)(durationSeconds * bitrateKbps * 1000 / 8);
        return estimated > 0 ? estimated : null;
    }

    public static FfmpegProgressData? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        Match sizeMatch = SizePattern().Match(line);
        Match timeMatch = TimePattern().Match(line);

        if (!sizeMatch.Success || !timeMatch.Success)
        {
            return null;
        }

        if (!long.TryParse(sizeMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sizeKb))
        {
            return null;
        }

        if (!int.TryParse(timeMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours) ||
            !int.TryParse(timeMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes) ||
            !int.TryParse(timeMatch.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) ||
            !int.TryParse(timeMatch.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int centiseconds))
        {
            return null;
        }

        long bytesDownloaded = sizeKb * 1024;
        TimeSpan time = new(0, hours, minutes, seconds, centiseconds * 10);

        return new FfmpegProgressData(bytesDownloaded, time);
    }
}
