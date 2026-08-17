using MongoDB.Driver;

namespace UrlShortener.Core.Mongo;

/// <summary>Creates the indexes the repository relies on. Safe to call on every startup.</summary>
public static class MongoInitializer
{
    public static void EnsureIndexes(IMongoDatabase database, MongoOptions options)
    {
        var urls = database.GetCollection<ShortUrlDocument>(options.ShortUrlsCollection);
        // Secondary index for idempotent-create lookups by destination URL (_id already covers code).
        urls.Indexes.CreateOne(new CreateIndexModel<ShortUrlDocument>(
            Builders<ShortUrlDocument>.IndexKeys.Ascending(x => x.LongUrl)));

        var clicks = database.GetCollection<ClickDocument>(options.ClicksCollection);
        clicks.Indexes.CreateOne(new CreateIndexModel<ClickDocument>(
            Builders<ClickDocument>.IndexKeys.Ascending(x => x.Code).Descending(x => x.OccurredAt)));
    }
}
