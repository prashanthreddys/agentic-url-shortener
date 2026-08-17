namespace UrlShortener.Api.Models;

/// <summary>Response body returned when a short link is created or fetched.</summary>
public sealed class ShortUrlResponse
{
    public string Code { get; set; } = string.Empty;
    public string ShortUrl { get; set; } = string.Empty;
    public string LongUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public long ClickCount { get; set; }
}

public sealed class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>A page of short links with paging metadata.</summary>
public sealed class ShortUrlListResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public IReadOnlyList<ShortUrlResponse> Items { get; set; } = Array.Empty<ShortUrlResponse>();
}
