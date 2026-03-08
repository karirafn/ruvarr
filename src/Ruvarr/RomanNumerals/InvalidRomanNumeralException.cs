namespace Ruvarr.RomanNumerals;

public class InvalidRomanNumeralException : Exception
{
    public InvalidRomanNumeralException()
        : base("Invalid roman numeral.")
    {
    }

    public InvalidRomanNumeralException(string literal)
        : base($"{literal} is not a valid roman numeral.")
    {
    }

    public InvalidRomanNumeralException(string literal, Exception innerException)
        : base($"{literal} is not a valid roman numeral.", innerException)
    {
    }
}