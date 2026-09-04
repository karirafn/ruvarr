using System.Text.RegularExpressions;

using Ruvarr.RomanNumerals;

namespace Ruvarr.Extensions;

internal static partial class StringExtensions
{
    internal static string? WithoutNumeralEnding(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        string[] parts = input.Split(' ');

        if (parts.Length < 2)
        {
            return input;
        }

        if (!RomanNumeral.TryParse(parts[^1], out _) && !int.TryParse(parts[^1], out _))
        {
            return input;
        }

        string output = string.Join(' ', parts[..^1]).Trim();

        return output;
    }

    internal static bool EqualsSanitized(this string a, string b) => a.Sanitized()
        .Equals(b.Sanitized(), StringComparison.OrdinalIgnoreCase);

    internal static string Sanitized(this string input) => input
        .Replace(".", string.Empty, StringComparison.OrdinalIgnoreCase)
        .RemoveSoftHyphens()
        .RemoveNonUnicodeCharacters()
        .RemoveExtraWhitespaces()
        .Trim();

    private static string RemoveNonUnicodeCharacters(this string input) => NonUnicodeCharactersRegex().Replace(input, " ");

    private static string RemoveExtraWhitespaces(this string input) => ExtraWhiteSpacesRegex().Replace(input, " ");

    // https://en.wikipedia.org/wiki/Soft_hyphen
    private static string RemoveSoftHyphens(this string input) => input.Replace("\u00AD", string.Empty, StringComparison.OrdinalIgnoreCase);

    internal static bool HasYearSuffix(this string? input) =>
        !string.IsNullOrWhiteSpace(input) && YearSuffixRegex().IsMatch(input);

    internal static string? WithoutYearSuffix(this string? input)
    {
        if (!input.HasYearSuffix())
        {
            return input;
        }

        return YearSuffixRegex().Replace(input!, string.Empty).Trim();
    }

    [GeneratedRegex(@"\(\d{4}\)\s*$")]
    private static partial Regex YearSuffixRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}]")]
    private static partial Regex NonUnicodeCharactersRegex();

    [GeneratedRegex(@" +")]
    private static partial Regex ExtraWhiteSpacesRegex();
}