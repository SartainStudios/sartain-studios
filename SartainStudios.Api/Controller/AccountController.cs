using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Account;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.User;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/account")]
public sealed class AccountController(AccountService accountService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<AccountResponse>> Get(CancellationToken cancellationToken)
    {
        return accountService.GetAsync(cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<AccountResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        return accountService.UpdateProfileAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPut("notification-preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<AccountResponse>> UpdateNotificationPreferences(
        [FromBody] NotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        return accountService.UpdateNotificationPreferencesAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost("password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<AccountResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        return accountService.ChangePasswordAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete("identities/{provider}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AccountResponse>> UnlinkIdentity(
        IdentityProvider provider,
        CancellationToken cancellationToken)
    {
        return accountService.UnlinkIdentityAsync(provider, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Delete([FromQuery] string userTimeZone, CancellationToken cancellationToken)
    {
        return accountService.DeleteAsync(userTimeZone, cancellationToken).ToActionResultAsync(this, NoContent);
    }
}