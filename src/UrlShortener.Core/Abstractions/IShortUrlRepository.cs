using UrlShortener.Core.Entities;

namespace UrlShortener.Core.Abstractions;

/// <summary>Persistence boundary for short URLs. Keeps the service independent of EF Core.</summary>
public interface IShortUrlRepository
{
    Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Finds an existing link for a destination URL, so creates can be idempotent.</summary>
    Task<ShortUrl?> GetByLongUrlAsync(string longUrl, CancellationToken ct = default);

    Task AddAsync(ShortUrl url, CancellationToken ct = default);

    /// <summary>Records a click and increments the link's click count (no change-tracking assumed).</summary>
    Task RecordClickAsync(ShortUrl link, ClickEvent click, CancellationToken ct = default);

    Task<bool> DeleteByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Returns a page of short links ordered newest first.</summary>
    Task<IReadOnlyList<ShortUrl>> ListAsync(int skip, int take, CancellationToken ct = default);

    /// <summary>Total number of short links.</summary>
    Task<int> CountAsync(CancellationToken ct = default);

    /// <summary>Returns the most recent click events for a code, newest first.</summary>
    Task<IReadOnlyList<ClickEvent>> GetRecentClicksAsync(string code, int limit, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
