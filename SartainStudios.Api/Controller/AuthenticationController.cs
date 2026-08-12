using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Schema.Authentication;

namespace SartainStudios.Api.Controller;

[ApiController]
[Route("api/authentication")]
[Produces("application/json")]
public sealed class AuthenticationController(AuthenticationService authenticationService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("google/sign-in")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Response>> GoogleSignIn(
        [FromBody] GoogleSignInRequest request,
        CancellationToken cancellationToken)
    {
        return authenticationService.GoogleSignInAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [AllowAnonymous]
    [HttpPost("email/register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Response>> Register(
        [FromBody] EmailRegisterRequest request,
        CancellationToken cancellationToken)
    {
        return authenticationService.RegisterAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [AllowAnonymous]
    [HttpPost("email/sign-in")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Response>> SignIn(
        [FromBody] EmailSignInRequest request,
        CancellationToken cancellationToken)
    {
        return authenticationService.SignInAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        return authenticationService.ForgotPasswordAsync(request, cancellationToken)
            .ToActionResultAsync(this, NoContent);
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        return authenticationService.ResetPasswordAsync(request, cancellationToken)
            .ToActionResultAsync(this, NoContent);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<Response>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        return authenticationService.RefreshAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [Authorize]
    [HttpPost("sign-out")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult> SignOut(
        [FromBody] SignOutRequest request,
        CancellationToken cancellationToken)
    {
        return authenticationService.SignOutAsync(request, cancellationToken).ToActionResultAsync(this, NoContent);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> Me()
    {
        return authenticationService.GetCurrentUser().ToActionResult(this);
    }
}