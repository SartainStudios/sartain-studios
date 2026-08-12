using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Onboarding;
using Status = SartainStudios.Schema.Onboarding.Status;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController(OnboardingService onboardingService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ActionResult<Status>> Get(CancellationToken cancellationToken)
    {
        return onboardingService.GetAsync(cancellationToken).ToActionResultAsync(this);
    }
}