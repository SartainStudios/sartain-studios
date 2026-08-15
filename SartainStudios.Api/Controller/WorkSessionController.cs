using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Timekeeping;
using SartainStudios.Schema.WorkSession;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/work-sessions")]
public sealed class WorkSessionController(WorkSessionService workSessionService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<IReadOnlyList<History>>> List(
        [FromQuery] string? contractId = null,
        [FromQuery] int take = 25,
        CancellationToken cancellationToken = default)
    {
        return workSessionService.ListAsync(contractId, take, cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("progress")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<IReadOnlyList<Progress>>> Progress(
        [FromQuery] string? contractId,
        CancellationToken cancellationToken)
    {
        return workSessionService.GetProgressAsync(contractId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("time-budget")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<TimeBudget>> GetTimeBudget(
        [FromQuery] DateTime dayStart,
        [FromQuery] DateTime dayEnd,
        [FromQuery] DateTime weekStart,
        [FromQuery] DateTime weekEnd,
        CancellationToken cancellationToken)
    {
        return workSessionService.GetTimeBudgetAsync(dayStart, dayEnd, weekStart, weekEnd, cancellationToken)
            .ToActionResultAsync(this);
    }

    [HttpGet("current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ActionResult<State>> GetCurrent(CancellationToken cancellationToken)
    {
        return workSessionService.GetCurrentAsync(cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<History>> Get(string id, CancellationToken cancellationToken)
    {
        return workSessionService.GetAsync(id, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<State>> Start([FromBody] StartRequest request, CancellationToken cancellationToken)
    {
        return workSessionService.StartAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<State>> Stop([FromBody] StopRequest request, CancellationToken cancellationToken)
    {
        return workSessionService.StopAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<History>> Update(
        string id,
        [FromBody] UpdateRequest request,
        [FromQuery] string userTimeZoneId,
        CancellationToken cancellationToken)
    {
        return workSessionService.UpdateAsync(id, request, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Discard(string id, [FromQuery] string userTimeZoneId, CancellationToken cancellationToken)
    {
        return workSessionService.DiscardAsync(id, userTimeZoneId, cancellationToken)
            .ToActionResultAsync(this, NoContent);
    }
}