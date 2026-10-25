using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Account;
using SartainStudios.Schema.User;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/administration/users")]
public sealed class UserAdministrationController(UserAdministrationService userAdministrationService) : ControllerBase
{
    [HttpGet("~/api/administration/access")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<bool>> HasAccess(CancellationToken cancellationToken)
    {
        return Ok(await userAdministrationService.IsAdministratorAsync(cancellationToken));
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<IReadOnlyList<AdministratorUserResponse>>> List(CancellationToken cancellationToken)
    {
        return userAdministrationService.ListAsync(cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AdministratorUserResponse>> Update(
        string id,
        [FromBody] AdministratorUpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        return userAdministrationService.UpdateAsync(id, request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Delete(
        string id,
        [FromQuery] string? userTimeZoneId,
        CancellationToken cancellationToken)
    {
        return userAdministrationService.DeleteAsync(id, userTimeZoneId, cancellationToken)
            .ToActionResultAsync(this, NoContent);
    }
}