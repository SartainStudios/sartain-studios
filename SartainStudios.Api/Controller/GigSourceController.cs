using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/gig/sources")]
public sealed class GigSourceController(GigSourceService gigSourceService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<Source>>> List(CancellationToken cancellationToken)
    {
        return gigSourceService.ListSourcesAsync(cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Source>> Create([FromBody] OptionRequest request, CancellationToken cancellationToken)
    {
        return gigSourceService.CreateSourceAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Source>> Update(
        string id,
        [FromBody] OptionRequest request,
        CancellationToken cancellationToken)
    {
        return gigSourceService.UpdateSourceAsync(id, request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut("{id}/hidden")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<Source>> SetHidden(
        string id,
        [FromBody] HiddenRequest request,
        CancellationToken cancellationToken)
    {
        return gigSourceService.SetSourceHiddenAsync(id, request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        return gigSourceService.DeleteSourceAsync(id, cancellationToken).ToActionResultAsync(this, NoContent);
    }
}