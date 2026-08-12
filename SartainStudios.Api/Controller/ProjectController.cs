using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Project;
using SartainStudios.Schema.Membership;
using CreateRequest = SartainStudios.Schema.Project.CreateRequest;
using Summary = SartainStudios.Schema.Project.Summary;
using UpdateRequest = SartainStudios.Schema.Project.UpdateRequest;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/projects")]
public sealed class ProjectController(ProjectService projectService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<Summary>>> List(CancellationToken cancellationToken)
    {
        return projectService.ListAsync(cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<Summary>> Get(string id, CancellationToken cancellationToken)
    {
        return projectService.GetAsync(id, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost]
    [Authorize(Roles = $"{nameof(RoleType.Owner)},{nameof(RoleType.Administrator)}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<Summary>> Create(
        [FromBody] CreateRequest request,
        CancellationToken cancellationToken)
    {
        return projectService.CreateAsync(request, cancellationToken)
            .ToActionResultAsync(this, summary => CreatedAtAction(nameof(Get), new { id = summary.Id }, summary));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{nameof(RoleType.Owner)},{nameof(RoleType.Administrator)}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<Summary>> Update(
        string id,
        [FromBody] UpdateRequest request,
        CancellationToken cancellationToken)
    {
        return projectService.UpdateAsync(id, request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{nameof(RoleType.Owner)},{nameof(RoleType.Administrator)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        return projectService.DeleteAsync(id, cancellationToken).ToActionResultAsync(this, NoContent);
    }
}