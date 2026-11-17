using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cms.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Application = "AI-Powered Humanitarian Crisis Management Platform",
            Timestamp = DateTime.UtcNow
        });
    }
}