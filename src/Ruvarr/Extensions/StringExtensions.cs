using System.Text.RegularExpressions;

namespace Ruvarr.Extensions;

internal static partial class StringExtensions
{
    private static readonly List<string> RomanNumerals = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX"];

    internal static string? WithoutRomanNumeralEnding(this string? input)
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

        if (!RomanNumerals.Contains(parts[^1]))
        {
            return input;
        }

        string output = string.Join(' ', parts[..^1]).Trim();

        return output;
    }

    internal static bool EqualsSanitized(this string a, string b) => a.RemovePunctiation()
        .Equals(b.RemovePunctiation(), StringComparison.OrdinalIgnoreCase);

    internal static string RemovePunctiation(this string input) => input
        .RemoveNonAlphaNumericCharacters()
        .RemoveExtraWhitespaces()
        .RemoveSoftHyphens();

    private static string RemoveNonAlphaNumericCharacters(this string input) => NonAlphaNumericCharactersRegex().Replace(input, " ");

    private static string RemoveExtraWhitespaces(this string input) => ExtraWhiteSpacesRegex().Replace(input, " ");

    // https://en.wikipedia.org/wiki/Soft_hyphen
    private static string RemoveSoftHyphens(this string input) => input.Replace("\u00AD", string.Empty, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"[^a-zA-Z0-9] ")]
    private static partial Regex NonAlphaNumericCharactersRegex();

    [GeneratedRegex(@" +")]
    private static partial Regex ExtraWhiteSpacesRegex();
}