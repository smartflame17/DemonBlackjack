using System;

public static class NumberFormatter
{
    private static readonly string[] suffixes =
    {
        "", "K", "M", "B", "T", "Q"
    };

    public static string Abbreviate(int value)
    {
        return Abbreviate((long)value);
    }

    public static string Abbreviate(long value)
    {
        int suffixIndex = 0;
        double abbreviatedValue = value;

        while (Math.Abs(abbreviatedValue) >= 1000 &&
               suffixIndex < suffixes.Length - 1)
        {
            abbreviatedValue /= 1000.0;
            suffixIndex++;
        }

        // Truncate to 2 decimal places instead of rounding.
        abbreviatedValue = Math.Truncate(abbreviatedValue * 100) / 100;

        return $"{abbreviatedValue:0.##}{suffixes[suffixIndex]}";
    }
}