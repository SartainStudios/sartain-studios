using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Extension;
using SartainStudios.Api.Schema.Invoice;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Schema.Invoice;
using SartainStudios.Schema.Membership;
using CreateRequest = SartainStudios.Schema.Invoice.CreateRequest;
using EditRequest = SartainStudios.Schema.Invoice.EditRequest;
using Summary = SartainStudios.Schema.Invoice.Summary;
using UpdateRequest = SartainStudios.Schema.Invoice.UpdateRequest;

namespace SartainStudios.Api.Controller;

[Authorize]
[ApiController]
[Route("api/invoices")]
public sealed class InvoiceController(InvoiceService invoiceService) : ControllerBase
{
    private const string ManagerRoles = $"{nameof(RoleType.Owner)},{nameof(RoleType.Administrator)}";

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<IReadOnlyList<Summary>>> List(
        CancellationToken cancellationToken,
        [FromQuery] string? clientId = null,
        [FromQuery] string? status = null,
        [FromQuery] int take = InvoiceErrors.MaximumTake)
    {
        return invoiceService.ListAsync(clientId, status, take, cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<Detail>> Get(
        string id,
        [FromQuery] string userTimeZoneId,
        CancellationToken cancellationToken)
    {
        return invoiceService.GetAsync(id, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("selectable-sessions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<IReadOnlyList<SelectableSession>>> SelectableSessions(
        [FromQuery] string contractId,
        CancellationToken cancellationToken)
    {
        return invoiceService.GetSelectableSessionsAsync(contractId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpGet("{id}/editable-sessions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<IReadOnlyList<SelectableSession>>> EditableSessions(
        string id,
        CancellationToken cancellationToken)
    {
        return invoiceService.GetEditableSessionsAsync(id, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPost]
    [Authorize(Roles = ManagerRoles)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Detail>> Generate(
        [FromBody] CreateRequest request,
        CancellationToken cancellationToken,
        [FromQuery] string userTimeZoneId)
    {
        return invoiceService.GenerateAsync(request, userTimeZoneId, cancellationToken)
            .ToActionResultAsync(this, detail => CreatedAtAction(nameof(Get), new { id = detail.Id }, detail));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = ManagerRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Detail>> Edit(
        string id,
        [FromBody] EditRequest request,
        [FromQuery] string userTimeZoneId,
        CancellationToken cancellationToken)
    {
        return invoiceService.EditAsync(id, request, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = ManagerRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        return invoiceService.DeleteAsync(id, cancellationToken).ToActionResultAsync(this, NoContent);
    }

    [HttpGet("{id}/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileContentResult))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<InvoiceDocument>> DownloadPdf(
        string id,
        [FromQuery] string userTimeZoneId,
        CancellationToken cancellationToken)
    {
        return invoiceService.RenderPdfAsync(id, userTimeZoneId, cancellationToken)
            .ToActionResultAsync(this, document => File(document.Content, document.ContentType, document.FileName));
    }

    [HttpPost("{id}/send")]
    [Authorize(Roles = ManagerRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Detail>> SendInvoice(
        string id,
        [FromQuery] string userTimeZoneId,
        CancellationToken cancellationToken)
    {
        return invoiceService.SendAsync(id, userTimeZoneId, cancellationToken).ToActionResultAsync(this);
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = ManagerRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<Detail>> UpdateStatus(
        string id,
        [FromBody] UpdateRequest request,
        [FromQuery] string userTimeZoneId,
        CancellationToken cancellationToken)
    {
        return invoiceService.UpdateStatusAsync(id, request, userTimeZoneId, cancellationToken)
            .ToActionResultAsync(this);
    }
}