using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/gig/reports")]
public sealed class GigReportController(GigReportService gigReportService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<Report>> Get(
        [FromQuery] int weeks = 12,
        [FromQuery] string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        return gigReportService.GetAsync(weeks, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }
}