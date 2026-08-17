namespace UrlShortener.Core.Services;

/// <summary>Tunable behavior for the shortening service.</summary>
public sealed class ShorteningOptions
{
    /// <summary>Length of generated codes.</summary>
    public int CodeLength { get; set; } = 7;

    /// <summary>Block links to private/loopback hosts (SSRF guardrail).</summary>
    public bool BlockPrivateHosts { get; set; } = true;

    /// <summary>Salt mixed into hashed client IPs for unique-visitor counting.</summary>
    public string IpHashSalt { get; set; } = "url-shortener-default-salt";
}
