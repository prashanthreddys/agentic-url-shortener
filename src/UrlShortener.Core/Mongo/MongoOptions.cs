namespace UrlShortener.Core.Mongo;

/// <summary>Connection settings for the MongoDB persistence provider.</summary>
public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "urlshortener";
    public string ShortUrlsCollection { get; set; } = "shortUrls";
    public string ClicksCollection { get; set; } = "clicks";
    public string CountersCollection { get; set; } = "counters";
}
