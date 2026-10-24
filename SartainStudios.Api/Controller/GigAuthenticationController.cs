using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Schema.Authentication;

namespace SartainStudios.Api.Controller;

[ApiController]
[Route("api/gig/authentication")]
[Produces("application/json")]
public sealed class GigAuthenticationController(GigAuthenticationService gigAuthenticationService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("google/sign-in")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<GigResponse>> GoogleSignIn(
        [FromBody] GigGoogleSignInRequest request,
        CancellationToken cancellationToken)
    {
        return gigAuthenticationService.GoogleSignInAsync(request, cancellationToken).ToActionResultAsync(this);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<GigResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        return gigAuthenticationService.RefreshAsync(request, cancellationToken).ToActionResultAsync(this);
    }
}