using System.Globalization;
using System.Text.RegularExpressions;

namespace Ruvarr.Infrastructure.FFmpeg;

internal static partial class FfmpegSceneParser
{
    [GeneratedRegex(@"pts_time:(\d+\.?\d*)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PtsTimePattern();

    [GeneratedRegex(@"^lavfi\.scene_score=(\d+\.?\d*)$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SceneScorePattern();

    public static double? TryParsePtsTime(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        Match match = PtsTimePattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;
    }

    public static double? TryParseSceneScore(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        Match match = SceneScorePattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;
    }
}
