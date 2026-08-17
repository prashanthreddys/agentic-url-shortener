using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Api.Models;
using UrlShortener.Core.Models;
using UrlShortener.Core.Services;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/urls")]
public sealed class UrlsController : ControllerBase
{
    private readonly UrlShorteningService _service;

    public UrlsController(UrlShorteningService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(ShortUrlListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.ListAsync(page, pageSize, ct);
        return Ok(new ShortUrlListResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            Total = result.Total,
            Items = result.Items.Select(ToResponse).ToList()
        });
    }

    [HttpPost]
    [EnableRateLimiting("create")]
    [ProducesResponseType(typeof(ShortUrlResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateShortUrlRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (!result.Success) return MapError(result.Error, result.Message!);

        var dto = result.Value!;
        return CreatedAtAction(nameof(Get), new { code = dto.Code }, ToResponse(dto));
    }

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ShortUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string code, CancellationToken ct)
    {
        var result = await _service.GetAsync(code, ct);
        return result.Success ? Ok(ToResponse(result.Value!)) : MapError(result.Error, result.Message!);
    }

    [HttpGet("{code}/stats")]
    [ProducesResponseType(typeof(UrlStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Stats(string code, CancellationToken ct)
    {
        var result = await _service.GetStatsAsync(code, 20, ct);
        return result.Success ? Ok(result.Value) : MapError(result.Error, result.Message!);
    }

    [HttpDelete("{code}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(code, ct);
        return result.Success ? NoContent() : MapError(result.Error, result.Message!);
    }

    private ShortUrlResponse ToResponse(ShortUrlDto dto)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return new ShortUrlResponse
        {
            Code = dto.Code,
            ShortUrl = $"{baseUrl}/{dto.Code}",
            LongUrl = dto.LongUrl,
            CreatedAt = dto.CreatedAt,
            ClickCount = dto.ClickCount
        };
    }

    private IActionResult MapError(UrlErrorCode code, string message)
    {
        var body = new ErrorResponse { Error = code.ToString(), Message = message };
        return code switch
        {
            UrlErrorCode.InvalidUrl => BadRequest(body),
            UrlErrorCode.NotFound => NotFound(body),
            UrlErrorCode.Disabled => StatusCode(StatusCodes.Status410Gone, body),
            UrlErrorCode.CodeGenerationFailed => StatusCode(StatusCodes.Status503ServiceUnavailable, body),
            _ => StatusCode(StatusCodes.Status500InternalServerError, body)
        };
    }
}
