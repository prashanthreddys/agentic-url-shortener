using UrlShortener.Core.Services;

namespace UrlShortener.Core.Tests;

public class CounterCodeGenerationTests
{
    [Fact]
    public void RangeProvider_yields_unique_contiguous_values_across_ranges()
    {
        var provider = new RangeCounterProvider(new InMemoryRangeAllocator(start: 0), rangeSize: 3);

        var values = Enumerable.Range(0, 10).Select(_ => provider.Next()).ToList();

        Assert.Equal(new long[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, values); // spans range boundaries
        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void Counter_codes_are_unique_and_at_least_7_chars()
    {
        var gen = new CounterShortCodeGenerator(
            new RangeCounterProvider(new InMemoryRangeAllocator(), rangeSize: 100));

        var codes = Enumerable.Range(0, 1000).Select(_ => gen.Generate(7)).ToList();

        Assert.All(codes, c => Assert.True(c.Length >= 7));
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void FileAllocator_does_not_reuse_ranges_across_restarts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"counter-{Guid.NewGuid():N}.dat");
        try
        {
            var first = new FileRangeAllocator(path, start: 0);
            var s1 = first.ReserveRange(1000);
            var s2 = first.ReserveRange(1000);

            // Simulate a restart: a new instance reads the persisted high-water mark.
            var afterRestart = new FileRangeAllocator(path, start: 0);
            var s3 = afterRestart.ReserveRange(1000);

            Assert.Equal(0, s1);
            Assert.Equal(1000, s2);
            Assert.Equal(2000, s3); // continues, never reuses a reserved range
        }
        finally
        {
            File.Delete(path);
        }
    }
}
