using UrlShortener.Core.Abstractions;
using UrlShortener.Core.Entities;

namespace UrlShortener.Core.Tests;

/// <summary>Minimal in-memory <see cref="IShortUrlRepository"/> for service unit tests.</summary>
internal sealed class InMemoryShortUrlRepository : IShortUrlRepository
{
    private readonly List<ShortUrl> _urls = new();
    private readonly List<ClickEvent> _clicks = new();
    private long _urlId;
    private long _clickId;

    public Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Task.FromResult(_urls.FirstOrDefault(u => u.Code == code));

    public Task<ShortUrl?> GetByLongUrlAsync(string longUrl, CancellationToken ct = default) =>
        Task.FromResult(_urls.OrderBy(u => u.CreatedAt).FirstOrDefault(u => u.LongUrl == longUrl));

    public Task AddAsync(ShortUrl url, CancellationToken ct = default)
    {
        url.Id = ++_urlId;
        _urls.Add(url);
        return Task.CompletedTask;
    }

    public Task RecordClickAsync(ShortUrl link, ClickEvent click, CancellationToken ct = default)
    {
        click.Id = ++_clickId;
        click.ShortUrlId = link.Id;
        link.ClickCount += 1;
        _clicks.Add(click);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteByCodeAsync(string code, CancellationToken ct = default)
    {
        var url = _urls.FirstOrDefault(u => u.Code == code);
        if (url is null) return Task.FromResult(false);
        _urls.Remove(url);
        _clicks.RemoveAll(c => c.ShortUrlId == url.Id);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<ShortUrl>> ListAsync(int skip, int take, CancellationToken ct = default)
    {
        var page = _urls
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToList();
        return Task.FromResult((IReadOnlyList<ShortUrl>)page);
    }

    public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(_urls.Count);

    public Task<IReadOnlyList<ClickEvent>> GetRecentClicksAsync(string code, int limit, CancellationToken ct = default)
    {
        var url = _urls.FirstOrDefault(u => u.Code == code);
        if (url is null) return Task.FromResult((IReadOnlyList<ClickEvent>)Array.Empty<ClickEvent>());
        var recent = _clicks.Where(c => c.ShortUrlId == url.Id)
            .OrderByDescending(c => c.OccurredAt)
            .Take(limit)
            .ToList();
        return Task.FromResult((IReadOnlyList<ClickEvent>)recent);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
}
