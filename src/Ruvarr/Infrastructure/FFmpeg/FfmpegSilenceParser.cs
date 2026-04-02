using System.Globalization;
using System.Text.RegularExpressions;

namespace Ruvarr.Infrastructure.FFmpeg;

internal static partial class FfmpegSilenceParser
{
    [GeneratedRegex(@"silence_start:\s*(\d+\.?\d*)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SilenceStartPattern();

    [GeneratedRegex(@"silence_end:\s*(\d+\.?\d*)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SilenceEndPattern();

    public static double? TryParseSilenceStart(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        Match match = SilenceStartPattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;
    }

    public static double? TryParseSilenceEnd(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        Match match = SilenceEndPattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;
    }
}
