using MongoDB.Driver;
using UrlShortener.Core.Abstractions;
using UrlShortener.Core.Services;

namespace UrlShortener.Core.Mongo;

/// <summary>
/// Distributed range allocator backed by an atomic counter document in MongoDB. Each reservation is
/// a single atomic <c>$inc</c>, so many application servers get disjoint ranges without a lock. This
/// is the same role Zookeeper plays in the reference design; MongoDB's atomic update provides it here.
/// </summary>
public sealed class MongoRangeAllocator : IRangeAllocator
{
    private const string CounterId = "shortcode";
    private readonly IMongoCollection<CounterDocument> _counters;

    public MongoRangeAllocator(IMongoDatabase database, MongoOptions options)
    {
        _counters = database.GetCollection<CounterDocument>(options.CountersCollection);
        EnsureSeeded();
    }

    public long ReserveRange(int size)
    {
        var updated = _counters.FindOneAndUpdate<CounterDocument>(
            x => x.Id == CounterId,
            Builders<CounterDocument>.Update.Inc(x => x.Seq, size),
            new FindOneAndUpdateOptions<CounterDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After });

        // Seq now points past the reserved block; the block starts `size` values earlier.
        return updated.Seq - size;
    }

    // Initialize the counter at the 7-character offset the first time only.
    private void EnsureSeeded() =>
        _counters.UpdateOne(
            x => x.Id == CounterId,
            Builders<CounterDocument>.Update.SetOnInsert(x => x.Seq, CounterShortCodeGenerator.SevenCharOffset),
            new UpdateOptions { IsUpsert = true });
}
