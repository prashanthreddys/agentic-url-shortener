using System.Text;

namespace UrlShortener.Core.Encoding;

/// <summary>
/// Base62 encoder (0-9, a-z, A-Z). Used to turn numeric ids into compact codes and available
/// as a deterministic alternative to random code generation.
/// </summary>
public static class Base62Encoder
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int Base = 62;

    public static string Encode(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative.");
        if (value == 0) return Alphabet[0].ToString();

        var sb = new StringBuilder();
        while (value > 0)
        {
            sb.Insert(0, Alphabet[(int)(value % Base)]);
            value /= Base;
        }
        return sb.ToString();
    }

    public static long Decode(string encoded)
    {
        if (string.IsNullOrEmpty(encoded)) throw new ArgumentException("Value required.", nameof(encoded));

        long result = 0;
        foreach (var c in encoded)
        {
            var idx = Alphabet.IndexOf(c);
            if (idx < 0) throw new FormatException($"Invalid base62 character '{c}'.");
            result = result * Base + idx;
        }
        return result;
    }

    public static bool IsValidCharacter(char c) => Alphabet.IndexOf(c) >= 0;
}
