using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/gig/categories")]
public sealed class GigCategoryController(GigSourceService gigSourceService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<Category>>> List(CancellationToken cancellationToken)
    {
        return gigSourceService.ListCategoriesAsync(cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Category>> Create([FromBody] OptionRequest request, CancellationToken cancellationToken)
    {
        return gigSourceService.CreateCategoryAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Category>> Update(
        string id,
        [FromBody] OptionRequest request,
        CancellationToken cancellationToken)
    {
        return gigSourceService.UpdateCategoryAsync(id, request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut("{id}/hidden")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<Category>> SetHidden(
        string id,
        [FromBody] HiddenRequest request,
        CancellationToken cancellationToken)
    {
        return gigSourceService.SetCategoryHiddenAsync(id, request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        return gigSourceService.DeleteCategoryAsync(id, cancellationToken).ToActionResultAsync(this, NoContent);
    }
}