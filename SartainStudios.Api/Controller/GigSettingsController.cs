using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/gig/settings")]
public sealed class GigSettingsController(GigSettingsService gigSettingsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ActionResult<Preferences>> Get(
        [FromQuery] string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        return gigSettingsService.GetAsync(userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<Preferences>> Update(
        [FromBody] UpdatePreferencesRequest request,
        [FromQuery] string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        return gigSettingsService.UpdateAsync(request, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }
}