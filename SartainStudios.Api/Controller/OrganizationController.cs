using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Organization;
using SartainStudios.Schema.Membership;
using SartainStudios.Schema.Organization;
using CreateRequest = SartainStudios.Schema.Organization.CreateRequest;
using UpdateRequest = SartainStudios.Schema.Organization.UpdateRequest;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/organizations")]
public sealed class OrganizationController(OrganizationService organizationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<OrganizationSummary>>> ListMine(CancellationToken cancellationToken)
    {
        return organizationService.ListMineAsync(cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<OrganizationSummary>> Create(
        [FromBody] CreateRequest request,
        CancellationToken cancellationToken)
    {
        return organizationService.CreateAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<OrganizationSummary>> Get(string id, CancellationToken cancellationToken)
    {
        return organizationService.GetAsync(id, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = nameof(RoleType.Owner))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<OrganizationSummary>> Update(
        string id,
        [FromBody] UpdateRequest request,
        CancellationToken cancellationToken)
    {
        return organizationService.UpdateAsync(id, request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost("{id}/switch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<SwitchResponse>> Switch(string id, CancellationToken cancellationToken)
    {
        return organizationService.SwitchAsync(id, cancellationToken).ToActionResultAsync(this);
    }
}