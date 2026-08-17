using System.Security.Cryptography;
using System.Text;
using UrlShortener.Core.Abstractions;
using UrlShortener.Core.Encoding;

namespace UrlShortener.Core.Services;

/// <summary>
/// Produces cryptographically random base62 codes. Random (not sequential) codes prevent
/// enumeration of other users' links.
/// </summary>
public sealed class RandomShortCodeGenerator : IShortCodeGenerator
{
    public string Generate(int length)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

        var sb = new StringBuilder(length);
        Span<byte> buffer = stackalloc byte[length];
        RandomNumberGenerator.Fill(buffer);
        foreach (var b in buffer)
        {
            // Modulo bias over 62 with a byte is negligible for non-cryptographic identifiers.
            var value = b % 62;
            sb.Append(ToBase62Char(value));
        }
        return sb.ToString();
    }

    private static char ToBase62Char(int value) => Base62Encoder.Encode(value)[0];
}
