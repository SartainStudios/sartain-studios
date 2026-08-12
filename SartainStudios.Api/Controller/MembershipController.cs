using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Membership;
using SartainStudios.Schema.Membership;
using InviteRequest = SartainStudios.Schema.Membership.InviteRequest;
using Summary = SartainStudios.Schema.Membership.Summary;
using UpdateRequest = SartainStudios.Schema.Membership.UpdateRequest;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/memberships")]
public sealed class MembershipController(MembershipService membershipService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<Summary>>> List(CancellationToken cancellationToken)
    {
        return membershipService.ListAsync(cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost]
    [Authorize(Roles = $"{nameof(RoleType.Owner)},{nameof(RoleType.Administrator)}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Summary>> Invite(
        [FromBody] InviteRequest request,
        CancellationToken cancellationToken)
    {
        return membershipService.InviteAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPatch("{id}/role")]
    [Authorize(Roles = nameof(RoleType.Owner))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Summary>> UpdateRole(
        string id,
        [FromBody] UpdateRequest request,
        CancellationToken cancellationToken)
    {
        return membershipService.UpdateRoleAsync(id, request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{nameof(RoleType.Owner)},{nameof(RoleType.Administrator)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Remove(string id, CancellationToken cancellationToken)
    {
        return membershipService.RemoveAsync(id, cancellationToken).ToActionResultAsync(this, NoContent);
    }

    [HttpPost("{id}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<Summary>> Accept(string id, CancellationToken cancellationToken)
    {
        return membershipService.AcceptAsync(id, cancellationToken).ToActionResultAsync(this);
    }
}