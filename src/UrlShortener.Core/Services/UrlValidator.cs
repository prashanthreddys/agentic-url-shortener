using UrlShortener.Core.Models;

namespace UrlShortener.Core.Services;

/// <summary>
/// Validates destination URLs. Enforces http(s) only and (optionally) blocks links to private /
/// loopback hosts to reduce SSRF and internal-network probing risk.
/// </summary>
public sealed class UrlValidator
{
    private const int MaxUrlLength = 2048;

    private readonly bool _blockPrivateHosts;

    public UrlValidator(bool blockPrivateHosts = true) => _blockPrivateHosts = blockPrivateHosts;

    public Result<Uri> ValidateDestination(string? longUrl)
    {
        if (string.IsNullOrWhiteSpace(longUrl))
            return Result<Uri>.Fail(UrlErrorCode.InvalidUrl, "URL is required.");

        if (longUrl.Length > MaxUrlLength)
            return Result<Uri>.Fail(UrlErrorCode.InvalidUrl, $"URL exceeds {MaxUrlLength} characters.");

        if (!Uri.TryCreate(longUrl, UriKind.Absolute, out var uri))
            return Result<Uri>.Fail(UrlErrorCode.InvalidUrl, "URL is not a valid absolute URI.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return Result<Uri>.Fail(UrlErrorCode.InvalidUrl, "Only http and https URLs are allowed.");

        if (_blockPrivateHosts && IsPrivateOrLoopback(uri))
            return Result<Uri>.Fail(UrlErrorCode.InvalidUrl, "URLs pointing to private or loopback hosts are not allowed.");

        return Result<Uri>.Ok(uri);
    }

    private static bool IsPrivateOrLoopback(Uri uri)
    {
        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;

        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip)) return true;
            var bytes = ip.GetAddressBytes();
            if (bytes.Length == 4)
            {
                // 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16
                if (bytes[0] == 10) return true;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                if (bytes[0] == 169 && bytes[1] == 254) return true;
            }
        }
        return false;
    }
}
