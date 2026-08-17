namespace UrlShortener.Core.Entities;

/// <summary>A single redirect/click recorded for analytics. Stores no raw PII (IP is hashed).</summary>
public class ClickEvent
{
    public long Id { get; set; }
    public long ShortUrlId { get; set; }
    public ShortUrl? ShortUrl { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? Referer { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Salted hash of the client IP, so unique-visitor stats work without storing raw IPs.</summary>
    public string? IpHash { get; set; }
}
