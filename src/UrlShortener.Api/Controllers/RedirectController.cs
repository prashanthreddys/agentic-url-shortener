using Microsoft.AspNetCore.Mvc;
using UrlShortener.Core.Models;
using UrlShortener.Core.Services;

namespace UrlShortener.Api.Controllers;

/// <summary>Public redirect endpoint. Resolves a short code and 302-redirects to the destination.</summary>
[ApiController]
public sealed class RedirectController : ControllerBase
{
    private readonly UrlShorteningService _service;

    public RedirectController(UrlShorteningService service) => _service = service;

    [HttpGet("/{code}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Resolve(string code, CancellationToken ct)
    {
        var context = new ClickContext
        {
            Referer = Request.Headers.Referer.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var result = await _service.ResolveAndRecordAsync(code, context, ct);
        if (result.Success)
            return Redirect(result.Value!);

        return result.Error switch
        {
            UrlErrorCode.NotFound => NotFound(),
            UrlErrorCode.Disabled => StatusCode(StatusCodes.Status410Gone),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
