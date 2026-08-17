using UrlShortener.Core.Models;
using UrlShortener.Core.Services;

namespace UrlShortener.Core.Tests;

public class UrlShorteningServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly InMemoryShortUrlRepository _repo = new();
    private readonly UrlShorteningService _service;

    public UrlShorteningServiceTests()
    {
        var options = new ShorteningOptions { CodeLength = 7 };
        _service = new UrlShorteningService(
            _repo, new RandomShortCodeGenerator(), new UrlValidator(options.BlockPrivateHosts), _clock, options);
    }

    [Fact]
    public async Task Create_generates_code_and_persists()
    {
        var result = await _service.CreateAsync(new CreateShortUrlRequest { LongUrl = "https://example.com" });

        Assert.True(result.Success);
        Assert.Equal(7, result.Value!.Code.Length);
    }

    [Fact]
    public async Task Create_is_idempotent_for_same_url()
    {
        var first = await _service.CreateAsync(new CreateShortUrlRequest { LongUrl = "https://example.com/very/long/path" });
        var second = await _service.CreateAsync(new CreateShortUrlRequest { LongUrl = "https://example.com/very/long/path" });

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.Value!.Code, second.Value!.Code); // same link reused, no duplicate

        var page = await _service.ListAsync(1, 50);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task Create_with_invalid_url_fails()
    {
        var result = await _service.CreateAsync(new CreateShortUrlRequest { LongUrl = "ftp://nope" });

        Assert.False(result.Success);
        Assert.Equal(UrlErrorCode.InvalidUrl, result.Error);
    }

    [Fact]
    public async Task Resolve_records_click_and_increments_count()
    {
        var created = await _service.CreateAsync(new CreateShortUrlRequest { LongUrl = "https://example.com/dest" });
        var code = created.Value!.Code;

        var resolve = await _service.ResolveAndRecordAsync(code, new ClickContext { IpAddress = "203.0.113.9" });

        Assert.True(resolve.Success);
        Assert.Equal("https://example.com/dest", resolve.Value);

        var fetched = await _service.GetAsync(code);
        Assert.Equal(1, fetched.Value!.ClickCount);
    }

    [Fact]
    public async Task Resolve_unknown_code_returns_not_found()
    {
        var resolve = await _service.ResolveAndRecordAsync("missing", new ClickContext());

        Assert.False(resolve.Success);
        Assert.Equal(UrlErrorCode.NotFound, resolve.Error);
    }

    [Fact]
    public async Task Stats_aggregate_clicks_and_unique_visitors()
    {
        var created = await _service.CreateAsync(new CreateShortUrlRequest { LongUrl = "https://example.com" });
        var code = created.Value!.Code;
        await _service.ResolveAndRecordAsync(code, new ClickContext { IpAddress = "203.0.113.1" });
        await _service.ResolveAndRecordAsync(code, new ClickContext { IpAddress = "203.0.113.1" });
        await _service.ResolveAndRecordAsync(code, new ClickContext { IpAddress = "203.0.113.2" });

        var stats = await _service.GetStatsAsync(code);

        Assert.True(stats.Success);
        Assert.Equal(3, stats.Value!.TotalClicks);
        Assert.Equal(2, stats.Value.UniqueVisitors);
    }

    [Fact]
    public async Task Delete_removes_link()
    {
        var created = await _service.CreateAsync(new CreateShortUrlRequest { LongUrl = "https://example.com" });
        var code = created.Value!.Code;

        var deleted = await _service.DeleteAsync(code);
        var fetched = await _service.GetAsync(code);

        Assert.True(deleted.Success);
        Assert.False(fetched.Success);
        Assert.Equal(UrlErrorCode.NotFound, fetched.Error);
    }

    [Fact]
    public async Task List_returns_links_newest_first_with_paging()
    {
        await _service.CreateAsync(new CreateShortUrlRequest { LongUrl = "https://example.com/1" });
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var c2 = await _service.CreateAsync(new CreateShortUrlRequest { LongUrl = "https://example.com/2" });
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var c3 = await _service.CreateAsync(new CreateShortUrlRequest { LongUrl = "https://example.com/3" });

        var page = await _service.ListAsync(page: 1, pageSize: 2);

        Assert.Equal(3, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(c3.Value!.Code, page.Items[0].Code); // newest first
        Assert.Equal(c2.Value!.Code, page.Items[1].Code);
    }
}
