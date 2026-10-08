using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application.Contracts;
using UrlShortener.Application.Services;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/urls")]
public class UrlsController : ControllerBase
{
    private readonly IUrlShortenerService _urlShortenerService;

    public UrlsController(IUrlShortenerService urlShortenerService)
    {
        _urlShortenerService = urlShortenerService;
    }

    [HttpPost]
    public async Task<ActionResult<ShortUrlResponse>> Create(
    [FromBody] CreateShortUrlRequest request,
    CancellationToken cancellationToken)
    {
        var result = await _urlShortenerService.CreateAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetAnalytics),
            new { shortCode = result.ShortCode },
            result);
    }

    [HttpGet("{shortCode}")]
    public async Task<IActionResult> Redirect(
        string shortCode,
        CancellationToken cancellationToken)
    {
        var originalUrl = await _urlShortenerService.ResolveAsync(
            shortCode,
            cancellationToken);

        if (originalUrl is null)
        {
            return NotFound(new
            {
                error = "Short URL was not found or has expired."
            });
        }

        return Redirect(originalUrl);
    }

    [HttpGet("{shortCode}/analytics")]
    public async Task<ActionResult<UrlAnalyticsResponse>> GetAnalytics(
        string shortCode,
        CancellationToken cancellationToken)
    {
        var result = await _urlShortenerService.GetAnalyticsAsync(
            shortCode,
            cancellationToken);

        if (result is null)
        {
            return NotFound(new
            {
                error = "Short URL was not found."
            });
        }

        return Ok(result);
    }

    [HttpDelete("{shortCode}")]
    public async Task<IActionResult> Delete(
        string shortCode,
        CancellationToken cancellationToken)
    {
        var deleted = await _urlShortenerService.DeleteAsync(
            shortCode,
            cancellationToken);

        if (!deleted)
        {
            return NotFound(new
            {
                error = "Short URL was not found."
            });
        }

        return NoContent();
    }
}