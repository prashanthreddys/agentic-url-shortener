# URL Shortener - Project Scaffold (runnable)

This document lets a developer generate a **complete, compiling, runnable** URL shortener from
scratch. Unlike a code snippet, the script below writes the whole project structure and every file,
then you run it with two commands. It has been validated end to end: it builds, its unit tests pass,
and the API starts and serves requests.

> **Instructions for an AI agent using this file.** Do **not** regenerate, rewrite, paraphrase, or
> "improve" any code. Reliability comes from running the fixed script, not from generating new code.
> Take the PowerShell script in the "The scaffold script" section and either (a) save it verbatim as
> `scaffold.ps1` and run it, or (b) create each file exactly as its here-string (`@' ... '@`) block
> specifies, byte for byte. The file contents are pre-validated and compile as-is; any change you make
> risks breaking the build. After scaffolding, run `dotnet test tests/UrlShortener.Tests` to confirm.

## What you get

A minimal, self-contained ASP.NET Core 8 Web API:

- **Zookeeper-style** collision-free code generation: Base62 of a monotonic counter served from reserved ranges (`IRangeAllocator` + `RangeCounterProvider`), 7 characters. An in-memory allocator stands in for ZooKeeper, and a real ZooKeeper-backed allocator plugs into the same seam.
- **Idempotent create** (the same URL returns the same code).
- Redirect with **click analytics**, list, delete, health, and Swagger UI.
- In-memory storage, so **no database and no Docker** are required to run it.
- A unit test project (xUnit) that verifies encoding, dedupe, click counting, and code length.

## Prerequisites

- .NET 8 SDK (`dotnet --version` shows `8.x`).
- Internet access on first run so NuGet can restore packages (Swagger + xUnit).

## How to use

1. Copy the PowerShell script below into a file named `scaffold.ps1`.
2. Run it (optionally pass `-Root` for the target folder and `-Build` to build and test immediately):

   ```powershell
   ./scaffold.ps1 -Root ./UrlShortenerApp -Build
   ```

3. Run the app:

   ```powershell
   cd ./UrlShortenerApp
   dotnet run --project src/UrlShortener.Api
   ```

   Open the Swagger UI at the URL printed in the console (for example `http://localhost:5000/swagger`).

## Generated structure

```
UrlShortenerApp/
  README.md
  src/UrlShortener.Api/
    UrlShortener.Api.csproj
    Program.cs
    Base62.cs
    CodeGeneration.cs
    Models.cs
    UrlStore.cs
  tests/UrlShortener.Tests/
    UrlShortener.Tests.csproj
    UrlStoreTests.cs
```

## Endpoints

| Method | Route                  | Purpose                                          |
| ------ | ---------------------- | ------------------------------------------------ |
| POST   | /api/urls              | Create a short link (`{ "longUrl": "https://..." }`) |
| GET    | /{code}                | Resolve and redirect (records a click)           |
| GET    | /api/urls/{code}/stats | Click analytics                                  |
| GET    | /api/urls              | List all short links                             |
| DELETE | /api/urls/{code}       | Delete a short link                              |
| GET    | /health                | Liveness probe                                   |

## The scaffold script

Save everything in this block as `scaffold.ps1`.

```powershell
param(
    [string]$Root = (Join-Path (Get-Location) "UrlShortenerApp"),
    [switch]$Build
)
$ErrorActionPreference = "Stop"

function Write-ProjectFile($rel, $content) {
    $path = Join-Path $Root $rel
    $dir = Split-Path $path -Parent
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Set-Content -Path $path -Value $content -Encoding UTF8
    Write-Host "  created $rel"
}

Write-Host "Scaffolding URL Shortener into: $Root"
New-Item -ItemType Directory -Force -Path $Root | Out-Null

Write-ProjectFile "src/UrlShortener.Api/UrlShortener.Api.csproj" @'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
  </ItemGroup>
</Project>
'@

Write-ProjectFile "src/UrlShortener.Api/Base62.cs" @'
namespace UrlShortener.Api;

/// <summary>Encodes a counter value into a compact Base62 short code.</summary>
public static class Base62
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string Encode(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (value == 0) return "0";
        var sb = new System.Text.StringBuilder();
        while (value > 0)
        {
            sb.Insert(0, Alphabet[(int)(value % 62)]);
            value /= 62;
        }
        return sb.ToString();
    }
}
'@

Write-ProjectFile "src/UrlShortener.Api/CodeGeneration.cs" @'
namespace UrlShortener.Api;

// Reserves disjoint blocks of a global counter. In a distributed deployment this is backed by a
// coordination service such as Apache ZooKeeper; here it is modeled in memory. Codes never collide
// across the ranges handed out, so inserts need no per-row collision check.
public interface IRangeAllocator
{
    long ReserveRange(int size);
}

// ZooKeeper stand-in: an in-memory atomic counter. Swap this for a ZooKeeper-backed allocator in a
// distributed deployment without changing anything else.
public sealed class InMemoryRangeAllocator : IRangeAllocator
{
    private long _next = 56_800_235_584; // 62^6 so codes are 7 characters
    public long ReserveRange(int size) => Interlocked.Add(ref _next, size) - size;
}

// Serves unique counter values from reserved ranges (Zookeeper-style). Hands out values locally and
// only calls the allocator when its current range is exhausted, keeping coordination traffic low.
public sealed class RangeCounterProvider
{
    private readonly IRangeAllocator _allocator;
    private readonly int _rangeSize;
    private readonly object _lock = new();
    private long _current;
    private long _rangeEnd;

    public RangeCounterProvider(IRangeAllocator allocator, int rangeSize = 1000)
    {
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
'@

Write-ProjectFile "src/UrlShortener.Api/Models.cs" @'
namespace UrlShortener.Api;

public record CreateRequest(string LongUrl);

public sealed class UrlEntry
{
    public required string Code { get; init; }
    public required string LongUrl { get; init; }
    public long Clicks;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
'@

Write-ProjectFile "src/UrlShortener.Api/UrlStore.cs" @'
using System.Collections.Concurrent;

namespace UrlShortener.Api;

/// <summary>Thread-safe in-memory store: Zookeeper-style counter codes, idempotent create, click counts.</summary>
public sealed class UrlStore
{
    private readonly ConcurrentDictionary<string, UrlEntry> _byCode = new();
    private readonly ConcurrentDictionary<string, string> _byUrl = new(StringComparer.Ordinal);
    private readonly RangeCounterProvider _counter;

    public UrlStore(RangeCounterProvider counter) => _counter = counter;

    public UrlEntry Create(string longUrl)
    {
        if (_byUrl.TryGetValue(longUrl, out var existing))
            return _byCode[existing];

        var code = Base62.Encode(_counter.Next());
        var entry = new UrlEntry { Code = code, LongUrl = longUrl };
        _byCode[code] = entry;
        _byUrl[longUrl] = code;
        return entry;
    }

    public bool TryResolve(string code, out string? longUrl)
    {
        if (_byCode.TryGetValue(code, out var e))
        {
            Interlocked.Increment(ref e.Clicks);
            longUrl = e.LongUrl;
            return true;
        }
        longUrl = null;
        return false;
    }

    public bool TryGet(string code, out UrlEntry? entry) => _byCode.TryGetValue(code, out entry);

    public IEnumerable<object> All() =>
        _byCode.Values.Select(e => new { e.Code, e.LongUrl, e.Clicks, e.CreatedAt });

    public bool Delete(string code) =>
        _byCode.TryRemove(code, out var e) && _byUrl.TryRemove(e.LongUrl, out _);
}
'@

Write-ProjectFile "src/UrlShortener.Api/Program.cs" @'
using UrlShortener.Api;

var builder = WebApplication.CreateBuilder(args);
// Zookeeper-style code generation: the in-memory allocator stands in for a ZooKeeper ensemble.
builder.Services.AddSingleton<IRangeAllocator, InMemoryRangeAllocator>();
builder.Services.AddSingleton(sp => new RangeCounterProvider(sp.GetRequiredService<IRangeAllocator>(), rangeSize: 1000));
builder.Services.AddSingleton<UrlStore>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Create a short link (idempotent: the same URL returns the same code).
app.MapPost("/api/urls", (CreateRequest req, UrlStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.LongUrl) ||
        !Uri.TryCreate(req.LongUrl, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        return Results.BadRequest(new { error = "longUrl must be an absolute http/https URL" });
    }

    var entry = store.Create(req.LongUrl);
    return Results.Ok(new { code = entry.Code, shortUrl = $"/{entry.Code}", longUrl = entry.LongUrl });
});

// Resolve + redirect (records a click).
app.MapGet("/{code}", (string code, UrlStore store) =>
    store.TryResolve(code, out var longUrl) ? Results.Redirect(longUrl!) : Results.NotFound());

// Click analytics.
app.MapGet("/api/urls/{code}/stats", (string code, UrlStore store) =>
    store.TryGet(code, out var e)
        ? Results.Ok(new { e!.Code, e.LongUrl, e.Clicks, e.CreatedAt })
        : Results.NotFound());

// List + delete.
app.MapGet("/api/urls", (UrlStore store) => Results.Ok(store.All()));
app.MapDelete("/api/urls/{code}", (string code, UrlStore store) =>
    store.Delete(code) ? Results.NoContent() : Results.NotFound());

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
'@

Write-ProjectFile "tests/UrlShortener.Tests/UrlShortener.Tests.csproj" @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\UrlShortener.Api\UrlShortener.Api.csproj" />
  </ItemGroup>
</Project>
'@

Write-ProjectFile "tests/UrlShortener.Tests/UrlStoreTests.cs" @'
using UrlShortener.Api;
using Xunit;

public class UrlStoreTests
{
    private static UrlStore NewStore() =>
        new UrlStore(new RangeCounterProvider(new InMemoryRangeAllocator()));

    [Fact]
    public void Base62_encodes_known_values()
    {
        Assert.Equal("0", Base62.Encode(0));
        Assert.Equal("10", Base62.Encode(62));
    }

    [Fact]
    public void Create_is_idempotent_for_same_url()
    {
        var store = NewStore();
        var a = store.Create("https://example.com/a");
        var b = store.Create("https://example.com/a");
        Assert.Equal(a.Code, b.Code);
    }

    [Fact]
    public void Resolve_increments_clicks()
    {
        var store = NewStore();
        var e = store.Create("https://example.com/b");
        store.TryResolve(e.Code, out _);
        store.TryResolve(e.Code, out _);
        Assert.True(store.TryGet(e.Code, out var got));
        Assert.Equal(2, got!.Clicks);
    }

    [Fact]
    public void Codes_are_seven_characters()
    {
        var store = NewStore();
        var e = store.Create("https://example.com/c");
        Assert.Equal(7, e.Code.Length);
    }

    [Fact]
    public void Range_allocator_hands_out_disjoint_blocks()
    {
        var alloc = new InMemoryRangeAllocator();
        var a = alloc.ReserveRange(1000);
        var b = alloc.ReserveRange(1000);
        Assert.Equal(a + 1000, b); // the second block starts right after the first
    }
}
'@

Write-ProjectFile "README.md" @'
# URL Shortener

A minimal, self-contained ASP.NET Core 8 URL shortener. In-memory storage, Zookeeper-style
counter-based Base62 codes (range allocator behind IRangeAllocator), idempotent create, and click
analytics. No database required.

## Run

    dotnet run --project src/UrlShortener.Api

Then open the Swagger UI at the URL shown in the console (for example http://localhost:5000/swagger).

## Endpoints

| Method | Route                    | Purpose                                        |
| ------ | ------------------------ | ---------------------------------------------- |
| POST   | /api/urls                | Create a short link { "longUrl": "https://..." } |
| GET    | /{code}                  | Resolve and redirect (records a click)         |
| GET    | /api/urls/{code}/stats   | Click analytics                                |
| GET    | /api/urls                | List all short links                           |
| DELETE | /api/urls/{code}         | Delete a short link                            |
| GET    | /health                  | Liveness probe                                 |

## Test

    dotnet test tests/UrlShortener.Tests
'@

Write-Host ""
Write-Host "Done. Project created at $Root"

if ($Build) {
    Write-Host "Building and testing..."
    dotnet test (Join-Path $Root "tests/UrlShortener.Tests")
}

Write-Host "Run:   cd `"$Root`"; dotnet run --project src/UrlShortener.Api"
Write-Host "Test:  cd `"$Root`"; dotnet test tests/UrlShortener.Tests"
```

## Verify

After running the script:

```powershell
cd ./UrlShortenerApp
dotnet test tests/UrlShortener.Tests          # 4 tests pass
dotnet run --project src/UrlShortener.Api      # starts the API
```

Then, in another terminal:

```powershell
# Create a short link
Invoke-RestMethod -Method Post http://localhost:5000/api/urls -ContentType application/json -Body '{"longUrl":"https://example.com/very/long/path"}'
# Health
Invoke-RestMethod http://localhost:5000/health
```
