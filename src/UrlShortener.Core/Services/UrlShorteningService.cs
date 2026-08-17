using System.Security.Cryptography;
using System.Text;
using UrlShortener.Core.Abstractions;
using UrlShortener.Core.Entities;
using UrlShortener.Core.Models;

namespace UrlShortener.Core.Services;

/// <summary>
/// Core business logic for creating, resolving, and reporting on short links. Independent of the
/// web host and of EF Core (talks to <see cref="IShortUrlRepository"/>).
/// </summary>
public sealed class UrlShorteningService
{
    private readonly IShortUrlRepository _repository;
    private readonly IShortCodeGenerator _codeGenerator;
    private readonly UrlValidator _validator;
    private readonly IClock _clock;
    private readonly ShorteningOptions _options;

    public UrlShorteningService(
        IShortUrlRepository repository,
        IShortCodeGenerator codeGenerator,
        UrlValidator validator,
        IClock clock,
        ShorteningOptions options)
    {
        _repository = repository;
        _codeGenerator = codeGenerator;
        _validator = validator;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<ShortUrlDto>> CreateAsync(CreateShortUrlRequest request, CancellationToken ct = default)
    {
        var destination = _validator.ValidateDestination(request.LongUrl);
        if (!destination.Success)
            return Result<ShortUrlDto>.Fail(destination.Error, destination.Message!);

        var normalizedUrl = destination.Value!.ToString();

        // Idempotent create: reuse the existing short link for a URL we have already shortened.
        var existing = await _repository.GetByLongUrlAsync(normalizedUrl, ct);
        if (existing is not null)
            return Result<ShortUrlDto>.Ok(ToDto(existing));

        // Counter-derived codes are globally unique by construction, so we insert directly without a
        // per-row collision check (see CounterShortCodeGenerator / ICounterRangeProvider).
        var entity = new ShortUrl
        {
            Code = _codeGenerator.Generate(_options.CodeLength),
            LongUrl = normalizedUrl,
            CreatedAt = _clock.UtcNow,
        };

        await _repository.AddAsync(entity, ct);
        await _repository.SaveChangesAsync(ct);
        return Result<ShortUrlDto>.Ok(ToDto(entity));
    }

    /// <summary>Resolves a code to its destination and records a click. Returns the destination URL.</summary>
    public async Task<Result<string>> ResolveAndRecordAsync(string code, ClickContext context, CancellationToken ct = default)
    {
        var entity = await _repository.GetByCodeAsync(code, ct);
        if (entity is null)
            return Result<string>.Fail(UrlErrorCode.NotFound, "Short link not found.");

        if (entity.IsDisabled)
            return Result<string>.Fail(UrlErrorCode.Disabled, "Short link is disabled.");

        var click = new ClickEvent
        {
            OccurredAt = _clock.UtcNow,
            Referer = Truncate(context.Referer, 512),
            UserAgent = Truncate(context.UserAgent, 512),
            IpHash = HashIp(context.IpAddress),
        };
        await _repository.RecordClickAsync(entity, click, ct);

        return Result<string>.Ok(entity.LongUrl);
    }

    public async Task<Result<ShortUrlDto>> GetAsync(string code, CancellationToken ct = default)
    {
        var entity = await _repository.GetByCodeAsync(code, ct);
        return entity is null
            ? Result<ShortUrlDto>.Fail(UrlErrorCode.NotFound, "Short link not found.")            : Result<ShortUrlDto>.Ok(ToDto(entity));
    }

    public async Task<Result<UrlStatsDto>> GetStatsAsync(string code, int recentLimit = 20, CancellationToken ct = default)
    {
        var entity = await _repository.GetByCodeAsync(code, ct);
        if (entity is null)
            return Result<UrlStatsDto>.Fail(UrlErrorCode.NotFound, "Short link not found.");

        var recent = await _repository.GetRecentClicksAsync(code, recentLimit, ct);
        var byReferer = recent
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Referer) ? "(direct)" : c.Referer!)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var stats = new UrlStatsDto
        {
            Code = entity.Code,
            LongUrl = entity.LongUrl,
            TotalClicks = entity.ClickCount,
            UniqueVisitors = recent.Where(c => c.IpHash is not null).Select(c => c.IpHash).Distinct().Count(),
            LastClickedAt = recent.Count > 0 ? recent[0].OccurredAt : null,
            RecentClicks = recent.Select(c => new ClickEventDto
            {
                OccurredAt = c.OccurredAt,
                Referer = c.Referer,
                UserAgent = c.UserAgent,
            }).ToList(),
            ClicksByReferer = byReferer,
        };
        return Result<UrlStatsDto>.Ok(stats);
    }

    /// <summary>Returns a page of created links, newest first. Page is 1-based; size is clamped to 1..100.</summary>
    public async Task<PagedResult<ShortUrlDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await _repository.CountAsync(ct);
        var items = await _repository.ListAsync((page - 1) * pageSize, pageSize, ct);
        return new PagedResult<ShortUrlDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items.Select(ToDto).ToList()
        };
    }

    public async Task<Result<bool>> DeleteAsync(string code, CancellationToken ct = default)
    {
        var deleted = await _repository.DeleteByCodeAsync(code, ct);
        if (!deleted)
            return Result<bool>.Fail(UrlErrorCode.NotFound, "Short link not found.");
        await _repository.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private string? HashIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(_options.IpHashSalt + ip));
        return Convert.ToHexString(bytes)[..16];
    }

    private static string? Truncate(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;

    private static ShortUrlDto ToDto(ShortUrl e) => new()
    {
        Code = e.Code,
        LongUrl = e.LongUrl,
        CreatedAt = e.CreatedAt,
        IsDisabled = e.IsDisabled,
        ClickCount = e.ClickCount,
    };
}
