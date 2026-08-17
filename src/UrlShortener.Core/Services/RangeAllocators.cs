using UrlShortener.Core.Abstractions;

namespace UrlShortener.Core.Services;

/// <summary>In-memory range allocator (single process, not durable). Used by tests.</summary>
public sealed class InMemoryRangeAllocator : IRangeAllocator
{
    private readonly object _lock = new();
    private long _next;

    public InMemoryRangeAllocator(long start = CounterShortCodeGenerator.SevenCharOffset) => _next = start;

    public long ReserveRange(int size)
    {
        lock (_lock)
        {
            var start = _next;
            _next += size;
            return start;
        }
    }
}

/// <summary>
/// File-backed range allocator. Persists the high-water mark so reserved ranges are never reused
/// across restarts, standing in for Zookeeper's durable, coordinated counter in a single-node setup.
/// </summary>
public sealed class FileRangeAllocator : IRangeAllocator
{
    private readonly string _path;
    private readonly object _lock = new();
    private long _next;

    public FileRangeAllocator(string path, long start = CounterShortCodeGenerator.SevenCharOffset)
    {
        _path = path;
        _next = File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), out var saved) ? saved : start;
    }

    public long ReserveRange(int size)
    {
        lock (_lock)
        {
            var start = _next;
            _next += size;
            File.WriteAllText(_path, _next.ToString());
            return start;
        }
    }
}
