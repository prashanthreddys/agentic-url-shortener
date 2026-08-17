using UrlShortener.Core.Models;
using UrlShortener.Core.Services;

namespace UrlShortener.Core.Tests;

public class UrlValidatorTests
{
    private readonly UrlValidator _validator = new(blockPrivateHosts: true);

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path?q=1")]
    public void Accepts_valid_public_http_urls(string url) =>
        Assert.True(_validator.ValidateDestination(url).Success);

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("")]
    public void Rejects_non_http_or_malformed(string url) =>
        Assert.False(_validator.ValidateDestination(url).Success);

    [Theory]
    [InlineData("http://localhost/admin")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://10.0.0.5")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public void Blocks_private_and_loopback_hosts_ssrf(string url)
    {
        var result = _validator.ValidateDestination(url);
        Assert.False(result.Success);
        Assert.Equal(UrlErrorCode.InvalidUrl, result.Error);
    }
}
