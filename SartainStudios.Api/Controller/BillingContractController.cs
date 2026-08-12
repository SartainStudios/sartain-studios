using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Service.Billing;
using SartainStudios.Schema.Membership;
using CreateRequest = SartainStudios.Schema.Billing.CreateRequest;
using Summary = SartainStudios.Schema.Billing.Summary;
using UpdateRequest = SartainStudios.Schema.Billing.UpdateRequest;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/billing-contracts")]
public sealed class BillingContractController(BillingContractService billingContractService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<IReadOnlyList<Summary>>> List(
        [FromQuery] string? projectId,
        CancellationToken cancellationToken)
    {
        return billingContractService.ListAsync(projectId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<Summary>> Get(string id, CancellationToken cancellationToken)
    {
        return billingContractService.GetAsync(id, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost]
    [Authorize(Roles = $"{nameof(RoleType.Owner)},{nameof(RoleType.Administrator)}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Summary>> Create(
        [FromBody] CreateRequest request,
        CancellationToken cancellationToken)
    {
        return billingContractService.CreateAsync(request, cancellationToken)
            .ToActionResultAsync(this, summary => CreatedAtAction(nameof(Get), new { id = summary.Id }, summary));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{nameof(RoleType.Owner)},{nameof(RoleType.Administrator)}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Summary>> Update(
        string id,
        [FromBody] UpdateRequest request,
        CancellationToken cancellationToken)
    {
        return billingContractService.UpdateAsync(id, request, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{nameof(RoleType.Owner)},{nameof(RoleType.Administrator)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        return billingContractService.DeleteAsync(id, cancellationToken).ToActionResultAsync(this, NoContent);
    }
}