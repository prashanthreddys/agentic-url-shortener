using UrlShortener.Core.Encoding;

namespace UrlShortener.Core.Tests;

public class Base62EncoderTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(61, "Z")]
    [InlineData(62, "10")]
    [InlineData(12345, "3d7")]
    public void Encode_produces_expected(long value, string expected) =>
        Assert.Equal(expected, Base62Encoder.Encode(value));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(62)]
    [InlineData(9_999_999_999)]
    public void Encode_then_Decode_roundtrips(long value) =>
        Assert.Equal(value, Base62Encoder.Decode(Base62Encoder.Encode(value)));

    [Fact]
    public void Encode_negative_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Base62Encoder.Encode(-1));

    [Fact]
    public void Decode_invalid_character_throws() =>
        Assert.Throws<FormatException>(() => Base62Encoder.Decode("abc$"));
}
