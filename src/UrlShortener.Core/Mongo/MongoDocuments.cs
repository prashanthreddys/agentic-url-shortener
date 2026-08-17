using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UrlShortener.Core.Mongo;

/// <summary>Short link document. The Base62 code is the natural primary key (_id).</summary>
public sealed class ShortUrlDocument
{
    [BsonId] public string Code { get; set; } = string.Empty;
    public string LongUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsDisabled { get; set; }
    public long ClickCount { get; set; }
}

/// <summary>Click event document, partitioned by the short code and sorted by time.</summary>
public sealed class ClickDocument
{
    [BsonId] public ObjectId Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? Referer { get; set; }
    public string? UserAgent { get; set; }
    public string? IpHash { get; set; }
}

/// <summary>Durable counter used to allocate short-code ranges (the Zookeeper analog).</summary>
public sealed class CounterDocument
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public long Seq { get; set; }
}
