using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.Api.Models;
using UrlShortener.Core.Models;

namespace UrlShortener.Api.Tests;

public class UrlApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UrlApiIntegrationTests(ApiFactory factory) => _factory = factory;

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = NewClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_201_with_code_and_short_url()
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/urls",
            new CreateShortUrlRequest { LongUrl = "https://example.com/a" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShortUrlResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Code));
        Assert.EndsWith($"/{body.Code}", body.ShortUrl);
    }

    [Fact]
    public async Task Redirect_returns_302_and_records_clicks_in_stats()
    {
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/urls",
            new CreateShortUrlRequest { LongUrl = "https://example.com/dest" });
        var code = (await created.Content.ReadFromJsonAsync<ShortUrlResponse>())!.Code;

        var redirect = await client.GetAsync($"/{code}");
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Equal("https://example.com/dest", redirect.Headers.Location!.ToString());

        await client.GetAsync($"/{code}"); // second click

        var stats = await client.GetFromJsonAsync<UrlStatsDto>($"/api/urls/{code}/stats");
        Assert.NotNull(stats);
        Assert.Equal(2, stats!.TotalClicks);
    }

    [Fact]
    public async Task Create_invalid_url_returns_400()
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/urls",
            new CreateShortUrlRequest { LongUrl = "ftp://nope" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ssrf_private_host_returns_400()
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/urls",
            new CreateShortUrlRequest { LongUrl = "http://169.254.169.254/latest/meta-data" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_code_redirect_returns_404()
    {
        var client = NewClient();
        var response = await client.GetAsync("/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_returns_created_links()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/api/urls", new CreateShortUrlRequest { LongUrl = "https://example.com/list-1" });
        await client.PostAsJsonAsync("/api/urls", new CreateShortUrlRequest { LongUrl = "https://example.com/list-2" });

        var list = await client.GetFromJsonAsync<ShortUrlListResponse>("/api/urls?page=1&pageSize=50");

        Assert.NotNull(list);
        Assert.True(list!.Total >= 2);
        Assert.Contains(list.Items, i => i.LongUrl == "https://example.com/list-1");
    }

    [Fact]
    public async Task Delete_then_get_returns_404()
    {
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/urls",
            new CreateShortUrlRequest { LongUrl = "https://example.com" });
        var code = (await created.Content.ReadFromJsonAsync<ShortUrlResponse>())!.Code;

        var delete = await client.DeleteAsync($"/api/urls/{code}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await client.GetAsync($"/api/urls/{code}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}
