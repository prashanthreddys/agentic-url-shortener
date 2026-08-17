using UrlShortener.Core.Abstractions;

namespace UrlShortener.Core.Services;

/// <summary>
/// Serves unique counter values from ranges reserved via an <see cref="IRangeAllocator"/>. It hands
/// out values locally (no coordination) until the current range is exhausted, then reserves the next
/// one. This is the pattern the reference design uses with Zookeeper to avoid code collisions.
/// </summary>
public sealed class RangeCounterProvider : ICounterRangeProvider
{
    private readonly IRangeAllocator _allocator;
    private readonly int _rangeSize;
    private readonly object _lock = new();
    private long _current;
    private long _rangeEnd;

    public RangeCounterProvider(IRangeAllocator allocator, int rangeSize = 1000)
    {
        if (rangeSize <= 0) throw new ArgumentOutOfRangeException(nameof(rangeSize));
        _allocator = allocator;
        _rangeSize = rangeSize;
    }

    public long Next()
    {
        lock (_lock)
        {
            if (_current >= _rangeEnd)
            {
                var start = _allocator.ReserveRange(_rangeSize);
                _current = start;
                _rangeEnd = start + _rangeSize;
            }
            return _current++;
        }
    }
}
