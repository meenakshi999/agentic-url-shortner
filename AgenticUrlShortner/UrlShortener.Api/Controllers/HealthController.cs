using Microsoft.AspNetCore.Mvc;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "UrlShortener.Api",
            timestampUtc = DateTime.UtcNow
        });
    }
}