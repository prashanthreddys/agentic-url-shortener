namespace UrlShortener.Core.Entities;

/// <summary>A shortened URL mapping. The <see cref="Code"/> is the public short identifier.</summary>
public class ShortUrl
{
    public long Id { get; set; }

    /// <summary>Public short code / alias used in the short link (e.g. "aZ3xK9").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The original destination URL.</summary>
    public string LongUrl { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Soft-disable flag; disabled links resolve to 410 Gone.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Denormalized running total kept in sync with <see cref="Clicks"/> for fast reads.</summary>
    public long ClickCount { get; set; }

    public ICollection<ClickEvent> Clicks { get; set; } = new List<ClickEvent>();
}
