using System.Diagnostics.CodeAnalysis;

namespace Ruvarr.RomanNumerals;

internal sealed partial class RomanNumeral
{
    private const int MaxRepeats = 3;
    private const char I = 'I';
    private const char V = 'V';
    private const char X = 'X';
    private const char L = 'L';
    private const char C = 'C';
    private const char D = 'D';
    private const char M = 'M';

    private static readonly Dictionary<char, int> Map = new()
    {
        [I] = 1,
        [V] = 5,
        [X] = 10,
        [L] = 50,
        [C] = 100,
        [D] = 500,
        [M] = 1000,
    };

    private static readonly char[] NonRepeating = [V, L, D];

    public string Literal { get; }

    public int Number { get; }

    private RomanNumeral(string literal, int number)
    {
        Literal = literal;
        Number = number;
    }

    internal static RomanNumeral Parse(string literal)
    {
        if (string.IsNullOrWhiteSpace(literal))
        {
            throw new InvalidRomanNumeralException(literal);
        }

        int number = 0;
        int repeats = 1;

        for (int i = 0; i < literal.Length; i++)
        {
            if (!Map.TryGetValue(literal[i], out int current))
            {
                throw new InvalidRomanNumeralException(literal);
            }

            if (i + 1 == literal.Length)
            {
                number += current;
                continue;
            }

            bool isNextRepated = literal[i] == literal[i + 1];
            repeats = isNextRepated ? repeats + 1 : 0;

            if (isNextRepated && (repeats > MaxRepeats || NonRepeating.Contains(literal[i])))
            {
                throw new InvalidRomanNumeralException(literal);
            }

            if (!Map.TryGetValue(literal[i + 1], out int next))
            {
                throw new InvalidRomanNumeralException(literal);
            }

            number += current < next ? -current : current;
        }

        return new RomanNumeral(literal, number);
    }

    internal static bool TryParse(string literal, [NotNullWhen(true)] out RomanNumeral? result)
    {
        try
        {
            result = Parse(literal);
            return true;
        }
        catch (InvalidRomanNumeralException)
        {
            result = null;
            return false;
        }
    }
}