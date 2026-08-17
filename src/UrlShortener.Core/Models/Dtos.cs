namespace UrlShortener.Core.Models;

/// <summary>Input for creating a short link.</summary>
public sealed class CreateShortUrlRequest
{
    public string LongUrl { get; set; } = string.Empty;
}

/// <summary>Public representation of a short link.</summary>
public sealed class ShortUrlDto
{
    public string Code { get; set; } = string.Empty;
    public string LongUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDisabled { get; set; }
    public long ClickCount { get; set; }
}

/// <summary>A page of results plus paging metadata.</summary>
public sealed class PagedResult<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}

public sealed class ClickEventDto
{
    public DateTimeOffset OccurredAt { get; set; }
    public string? Referer { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>Analytics view for a short link.</summary>
public sealed class UrlStatsDto
{
    public string Code { get; set; } = string.Empty;
    public string LongUrl { get; set; } = string.Empty;
    public long TotalClicks { get; set; }
    public long UniqueVisitors { get; set; }
    public DateTimeOffset? LastClickedAt { get; set; }
    public IReadOnlyList<ClickEventDto> RecentClicks { get; set; } = Array.Empty<ClickEventDto>();
    public IReadOnlyDictionary<string, long> ClicksByReferer { get; set; } = new Dictionary<string, long>();
}

/// <summary>Context captured at redirect time for analytics. All fields optional.</summary>
public sealed class ClickContext
{
    public string? Referer { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}
