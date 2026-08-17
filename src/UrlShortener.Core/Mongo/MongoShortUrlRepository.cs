using MongoDB.Driver;
using UrlShortener.Core.Abstractions;
using UrlShortener.Core.Entities;

namespace UrlShortener.Core.Mongo;

/// <summary>
/// MongoDB-backed <see cref="IShortUrlRepository"/>. Reads/writes are single-key operations on the
/// short code (_id), which is NoSQL's sweet spot for a redirect service. Deduplication uses a
/// secondary index on the long URL.
/// </summary>
public sealed class MongoShortUrlRepository : IShortUrlRepository
{
    private readonly IMongoCollection<ShortUrlDocument> _urls;
    private readonly IMongoCollection<ClickDocument> _clicks;

    public MongoShortUrlRepository(IMongoDatabase database, MongoOptions options)
    {
        _urls = database.GetCollection<ShortUrlDocument>(options.ShortUrlsCollection);
        _clicks = database.GetCollection<ClickDocument>(options.ClicksCollection);
    }

    public async Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var doc = await _urls.Find(x => x.Code == code).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToEntity(doc);
    }

    public async Task<ShortUrl?> GetByLongUrlAsync(string longUrl, CancellationToken ct = default)
    {
        var doc = await _urls.Find(x => x.LongUrl == longUrl)
            .SortBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        return doc is null ? null : ToEntity(doc);
    }

    public Task AddAsync(ShortUrl url, CancellationToken ct = default) =>
        _urls.InsertOneAsync(ToDocument(url), cancellationToken: ct);

    public async Task RecordClickAsync(ShortUrl link, ClickEvent click, CancellationToken ct = default)
    {
        await _clicks.InsertOneAsync(new ClickDocument
        {
            Code = link.Code,
            OccurredAt = click.OccurredAt.UtcDateTime,
            Referer = click.Referer,
            UserAgent = click.UserAgent,
            IpHash = click.IpHash,
        }, cancellationToken: ct);

        await _urls.UpdateOneAsync(
            x => x.Code == link.Code,
            Builders<ShortUrlDocument>.Update.Inc(x => x.ClickCount, 1),
            cancellationToken: ct);
    }

    public async Task<bool> DeleteByCodeAsync(string code, CancellationToken ct = default)
    {
        var result = await _urls.DeleteOneAsync(x => x.Code == code, ct);
        if (result.DeletedCount == 0) return false;
        await _clicks.DeleteManyAsync(x => x.Code == code, ct);
        return true;
    }

    public async Task<IReadOnlyList<ShortUrl>> ListAsync(int skip, int take, CancellationToken ct = default)
    {
        var docs = await _urls.Find(FilterDefinition<ShortUrlDocument>.Empty)
            .SortByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(ct);
        return docs.Select(ToEntity).ToList();
    }

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        (int)await _urls.CountDocumentsAsync(FilterDefinition<ShortUrlDocument>.Empty, cancellationToken: ct);

    public async Task<IReadOnlyList<ClickEvent>> GetRecentClicksAsync(string code, int limit, CancellationToken ct = default)
    {
        var docs = await _clicks.Find(x => x.Code == code)
            .SortByDescending(x => x.OccurredAt)
            .Limit(limit)
            .ToListAsync(ct);
        return docs.Select(d => new ClickEvent
        {
            OccurredAt = new DateTimeOffset(DateTime.SpecifyKind(d.OccurredAt, DateTimeKind.Utc)),
            Referer = d.Referer,
            UserAgent = d.UserAgent,
            IpHash = d.IpHash,
        }).ToList();
    }

    // Mongo writes are applied immediately; there is no unit-of-work to flush.
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

    private static ShortUrl ToEntity(ShortUrlDocument d) => new()
    {
        Code = d.Code,
        LongUrl = d.LongUrl,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc)),
        IsDisabled = d.IsDisabled,
        ClickCount = d.ClickCount,
    };

    private static ShortUrlDocument ToDocument(ShortUrl e) => new()
    {
        Code = e.Code,
        LongUrl = e.LongUrl,
        CreatedAt = e.CreatedAt.UtcDateTime,
        IsDisabled = e.IsDisabled,
        ClickCount = e.ClickCount,
    };
}
