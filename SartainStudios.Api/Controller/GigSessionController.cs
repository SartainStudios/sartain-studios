using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/gig/sessions")]
public sealed class GigSessionController(GigSessionService gigSessionService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<IReadOnlyList<GigSessionDto>>> List(
        [FromQuery] int take = 50,
        [FromQuery] string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        return gigSessionService.ListAsync(take, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ActionResult<GigSessionState>> GetCurrent(
        [FromQuery] string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        return gigSessionService.GetCurrentAsync(userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GigSessionDto>> Get(
        string id,
        [FromQuery] string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        return gigSessionService.GetAsync(id, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<GigSessionState>> Start(
        [FromBody] StartGigSessionRequest request,
        [FromQuery] string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        return gigSessionService.StartAsync(request, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GigSessionDto>> Stop(
        [FromBody] StopGigSessionRequest request,
        [FromQuery] string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        return gigSessionService.StopAsync(request, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<GigSessionDto>> Create(
        [FromBody] GigSessionRequest request,
        [FromQuery] string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        return gigSessionService.CreateAsync(request, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GigSessionDto>> Update(
        string id,
        [FromBody] GigSessionRequest request,
        [FromQuery] string? userTimeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        return gigSessionService.UpdateAsync(id, request, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        return gigSessionService.DeleteAsync(id, cancellationToken).ToActionResultAsync(this, NoContent);
    }
}