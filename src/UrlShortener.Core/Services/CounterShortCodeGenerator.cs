using UrlShortener.Core.Abstractions;
using UrlShortener.Core.Encoding;

namespace UrlShortener.Core.Services;

/// <summary>
/// Derives short codes by Base62-encoding a unique counter value (from <see cref="ICounterRangeProvider"/>).
/// Because each counter value is unique, codes never collide and can be inserted without a DB check.
/// The counter starts at an offset so codes are at least 7 characters.
/// </summary>
public sealed class CounterShortCodeGenerator : IShortCodeGenerator
{
    /// <summary>62^6: the smallest counter value whose Base62 form is 7 characters ("1000000").</summary>
    public const long SevenCharOffset = 56_800_235_584;

    private readonly ICounterRangeProvider _counter;

    public CounterShortCodeGenerator(ICounterRangeProvider counter) => _counter = counter;

    public string Generate(int length)
    {
        var code = Base62Encoder.Encode(_counter.Next());
        return code.Length >= length ? code : code.PadLeft(length, '0');
    }
}
