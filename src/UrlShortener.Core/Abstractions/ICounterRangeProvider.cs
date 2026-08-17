namespace UrlShortener.Core.Abstractions;

/// <summary>
/// Hands out globally-unique, monotonically increasing counter values used to derive short codes.
/// This models the role Apache Zookeeper plays in a distributed deployment: each application server
/// reserves a disjoint RANGE of the counter, so generated codes never collide across servers and the
/// insert path needs no per-row collision check.
/// </summary>
public interface ICounterRangeProvider
{
    long Next();
}

/// <summary>
/// Reserves disjoint blocks of the global counter. In a distributed system this is backed by a
/// coordination service (Zookeeper); locally it is backed by a persisted file. A server calls this
/// only when it exhausts its current in-memory range, keeping coordination traffic low.
/// </summary>
public interface IRangeAllocator
{
    /// <summary>Atomically reserves a block of <paramref name="size"/> values and returns its start.</summary>
    long ReserveRange(int size);
}
