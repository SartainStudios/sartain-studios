using System.Net.Mail;
using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Schema.Invoice;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Notification;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Invoice;
using CreateRequest = SartainStudios.Schema.Invoice.CreateRequest;
using EditRequest = SartainStudios.Schema.Invoice.EditRequest;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using Status = SartainStudios.Schema.Invoice.Status;
using Summary = SartainStudios.Schema.Invoice.Summary;
using UpdateRequest = SartainStudios.Schema.Invoice.UpdateRequest;

namespace SartainStudios.Api.Service.Invoice;

public sealed class InvoiceService(
    Database database,
    Access access,
    IMongoClient mongoClient,
    Assignment assignment,
    Sequence sequence,
    IEmail email,
    TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<Summary>>> ListAsync(
        string? clientId = null,
        string? status = null,
        int take = InvoiceErrors.MaximumTake,
        CancellationToken cancellationToken = default)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        var query = ValidateQuery(clientId, status, take);
        if (query.IsFailure)
            return query.Error;
        var filter = Builders<InvoiceEntity>.Filter.Eq(invoice => invoice.OrganizationId, context.Value.OrganizationId);
        if (query.Value.ClientId is { } parsedClientId)
            filter &= Builders<InvoiceEntity>.Filter.Eq(invoice => invoice.ClientId, parsedClientId);
        if (query.Value.Status is { } normalizedStatus)
            filter &= Builders<InvoiceEntity>.Filter.Eq(invoice => invoice.Status, normalizedStatus);
        var invoices = await database.Invoices
            .Find(filter)
            .Sort(Builders<InvoiceEntity>.Sort.Descending(invoice => invoice.CreatedAt))
            .Limit(take)
            .ToListAsync(cancellationToken);
        IReadOnlyList<Summary> summaries = invoices.Select(Presentation.ToSummary).ToList();
        return Result.Success(summaries);
    }

    public async Task<Result<Detail>> GetAsync(
        string id,
        string userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(userTimeZoneId);
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        return await BuildDetailAsync(loaded.Value, userTimeZone);
    }

    public async Task<Result<IReadOnlyList<SelectableSession>>> GetSelectableSessionsAsync(
        string contractId,
        CancellationToken cancellationToken = default)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(contractId, out var parsedContractId))
            return InvoiceErrors.InvalidContractId;
        return await BuildSelectableSessionsAsync(
            context.Value.OrganizationId, parsedContractId, null, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectableSession>>> GetEditableSessionsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        var invoice = loaded.Value.Invoice;
        if (!Lifecycle.IsDraft(invoice.Status))
            return InvoiceErrors.NotEditable;
        if (!ObjectId.TryParse(invoice.ProjectSnapshot.ContractId, out var contractId))
            return InvoiceErrors.MissingContractReference;
        return await BuildSelectableSessionsAsync(
            loaded.Value.OrganizationId, contractId, loaded.Value.InvoiceId, cancellationToken);
    }

    public async Task<Result<Detail>> GenerateAsync(
        CreateRequest request,
        string userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(userTimeZoneId);
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        var validation = Selection.ValidateRequest(request.ContractId, request.SessionIds, request.DueDate);
        if (validation.IsFailure)
            return validation.Error;
        var organizationId = context.Value.OrganizationId;
        var sessionIds = validation.Value.SessionIds;
        using var mongoSession = await mongoClient.StartSessionAsync(cancellationToken: cancellationToken);
        mongoSession.StartTransaction();
        try
        {
            var contract = await database.BillingContracts
                .Find(mongoSession,
                    entity => entity.Id == validation.Value.ContractId && entity.OrganizationId == organizationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (contract is null)
                return await AbortAsync(mongoSession, InvoiceErrors.ContractNotFound, cancellationToken);
            var project = await database.Projects
                .Find(mongoSession,
                    entity => entity.Id == contract.ProjectId && entity.OrganizationId == organizationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (project is null)
                return await AbortAsync(mongoSession, InvoiceErrors.ProjectNotFound, cancellationToken);
            var client = await database.Clients
                .Find(mongoSession, entity => entity.Id == project.ClientId && entity.OrganizationId == organizationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (client is null)
                return await AbortAsync(mongoSession, InvoiceErrors.ClientNotFound, cancellationToken);
            var organization = await database.Organizations
                .Find(mongoSession, entity => entity.Id == organizationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (organization is null)
                return await AbortAsync(mongoSession, InvoiceErrors.OrganizationNotFound, cancellationToken);
            var sessions = await assignment.LoadSelectedSessionsAsync(
                mongoSession, organizationId, contract.Id, sessionIds);
            var selection = Selection.Validate(sessions, sessionIds, contract);
            if (selection.IsFailure)
                return await AbortAsync(mongoSession, selection.Error, cancellationToken);
            var allocated = await sequence.AllocateAsync(mongoSession, organizationId, contract.InvoicePrefix);
            if (allocated is null)
                return await AbortAsync(mongoSession, InvoiceErrors.NumberUnavailable, cancellationToken);
            var totals = Totals.Calculate(sessions, contract.HourlyRate, userTimeZone);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var invoice = new InvoiceEntity
            {
                OrganizationId = organizationId,
                ClientId = client.Id,
                InvoiceNumber = Sequence.BuildInvoiceNumber(contract.InvoicePrefix, allocated),
                DueDate = request.DueDate,
                OrganizationSnapshot = Presentation.ToOrganizationSnapshot(organization),
                ClientSnapshot = Presentation.ToClientSnapshot(client),
                ProjectSnapshot = Presentation.ToProjectSnapshot(project, contract),
                TotalAmount = totals.TotalAmount,
                TotalMinutesWorked = totals.TotalMinutesWorked,
                TotalDaysWorked = totals.TotalDaysWorked,
                AverageRevenuePerDay = totals.AverageRevenuePerDay,
                Status = nameof(Status.Draft),
                BilledSessionIds = sessions.Select(session => session.Id).ToArray(),
                CreatedAt = now,
                UpdatedAt = now
            };
            if (!await assignment.AssignAsync(
                    mongoSession, organizationId, invoice.Id, invoice.BilledSessionIds, now))
                return await AbortAsync(mongoSession, InvoiceErrors.SessionsChanged, cancellationToken);
            await database.Invoices.InsertOneAsync(mongoSession, invoice, cancellationToken: cancellationToken);
            await mongoSession.CommitTransactionAsync(cancellationToken);
            return Presentation.ToDetail(invoice, sessions, userTimeZone);
        }
        catch (Exception exception) when (exception is MongoException or InvalidOperationException)
        {
            return await AbortAsync(mongoSession, InvoiceErrors.GenerationConflict, cancellationToken);
        }
    }

    public async Task<Result<Detail>> EditAsync(
        string id,
        EditRequest request,
        string userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(userTimeZoneId);
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var invoiceId))
            return InvoiceErrors.InvalidId;
        var validation = Selection.ValidateRequest(request.SessionIds, request.DueDate);
        if (validation.IsFailure)
            return validation.Error;
        var organizationId = context.Value.OrganizationId;
        var sessionIds = validation.Value;
        using var mongoSession = await mongoClient.StartSessionAsync(cancellationToken: cancellationToken);
        mongoSession.StartTransaction();
        try
        {
            var invoice = await database.Invoices
                .Find(mongoSession, entity => entity.Id == invoiceId && entity.OrganizationId == organizationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (invoice is null)
                return await AbortAsync(mongoSession, InvoiceErrors.NotFound(id), cancellationToken);
            if (!Lifecycle.IsDraft(invoice.Status))
                return await AbortAsync(mongoSession, InvoiceErrors.NotEditable, cancellationToken);
            if (!ObjectId.TryParse(invoice.ProjectSnapshot.ContractId, out var contractId))
                return await AbortAsync(mongoSession, InvoiceErrors.MissingContractReference, cancellationToken);
            var contract = await database.BillingContracts
                .Find(mongoSession, entity => entity.Id == contractId && entity.OrganizationId == organizationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (contract is null)
                return await AbortAsync(mongoSession, InvoiceErrors.ContractNotFound, cancellationToken);
            var sessions = await assignment.LoadSelectedSessionsAsync(
                mongoSession, organizationId, contractId, sessionIds);
            var selection = Selection.Validate(sessions, sessionIds, contract, invoiceId);
            if (selection.IsFailure)
                return await AbortAsync(mongoSession, selection.Error, cancellationToken);
            var newSessionIds = sessions.Select(session => session.Id).ToArray();
            var now = timeProvider.GetUtcNow().UtcDateTime;
            await assignment.ReleaseAsync(
                mongoSession, organizationId, invoiceId, invoice.BilledSessionIds.Except(newSessionIds).ToArray(), now);
            if (!await assignment.AssignAsync(
                    mongoSession, organizationId, invoiceId,
                    newSessionIds.Except(invoice.BilledSessionIds).ToArray(), now))
                return await AbortAsync(mongoSession, InvoiceErrors.SessionsChanged, cancellationToken);
            var totals = Totals.Calculate(sessions, contract.HourlyRate, userTimeZone);
            invoice.DueDate = request.DueDate;
            invoice.BilledSessionIds = newSessionIds;
            invoice.TotalAmount = totals.TotalAmount;
            invoice.TotalMinutesWorked = totals.TotalMinutesWorked;
            invoice.TotalDaysWorked = totals.TotalDaysWorked;
            invoice.AverageRevenuePerDay = totals.AverageRevenuePerDay;
            invoice.UpdatedAt = now;
            await database.Invoices.ReplaceOneAsync(
                mongoSession, entity => entity.Id == invoiceId, invoice, cancellationToken: cancellationToken);
            await mongoSession.CommitTransactionAsync(cancellationToken);
            return Presentation.ToDetail(invoice, sessions, userTimeZone);
        }
        catch (Exception exception) when (exception is MongoException or InvalidOperationException)
        {
            return await AbortAsync(mongoSession, InvoiceErrors.UpdateConflict, cancellationToken);
        }
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var invoiceId))
            return InvoiceErrors.InvalidId;
        var organizationId = context.Value.OrganizationId;
        using var mongoSession = await mongoClient.StartSessionAsync(cancellationToken: cancellationToken);
        mongoSession.StartTransaction();
        try
        {
            var invoice = await database.Invoices
                .Find(mongoSession, entity => entity.Id == invoiceId && entity.OrganizationId == organizationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (invoice is null)
                return await AbortAsync(mongoSession, InvoiceErrors.NotFound(id), cancellationToken);
            if (!Lifecycle.IsDraft(invoice.Status))
                return await AbortAsync(mongoSession, InvoiceErrors.NotDeletable, cancellationToken);
            await assignment.ReleaseAsync(
                mongoSession, organizationId, invoiceId, invoice.BilledSessionIds,
                timeProvider.GetUtcNow().UtcDateTime);
            await sequence.TryRollBackAsync(mongoSession, organizationId, invoice);
            await database.Invoices.DeleteOneAsync(
                mongoSession, entity => entity.Id == invoiceId, cancellationToken: cancellationToken);
            await mongoSession.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (exception is MongoException or InvalidOperationException)
        {
            return await AbortAsync(mongoSession, InvoiceErrors.DeletionConflict, cancellationToken);
        }
    }

    public async Task<Result<InvoiceDocument>> RenderPdfAsync(
        string id,
        string userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(userTimeZoneId);
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        var sessions = await assignment.LoadBilledSessionsAsync(loaded.Value.OrganizationId, loaded.Value.InvoiceId);
        var detail = Presentation.ToDetail(loaded.Value.Invoice, sessions, userTimeZone);
        return new InvoiceDocument(
            Document.FileName(loaded.Value.Invoice.InvoiceNumber),
            Document.ContentType,
            Document.Render(detail, sessions));
    }

    public async Task<Result<Detail>> SendAsync(
        string id,
        string userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(userTimeZoneId);
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        var invoice = loaded.Value.Invoice;
        if (string.IsNullOrWhiteSpace(invoice.ClientSnapshot.Email))
            return InvoiceErrors.ClientEmailMissing;
        var sessions = await assignment.LoadBilledSessionsAsync(loaded.Value.OrganizationId, loaded.Value.InvoiceId);
        var detail = Presentation.ToDetail(invoice, sessions, userTimeZone);
        try
        {
            email.SendEmail(InvoiceEmail.Build(invoice, detail, Document.Render(detail, sessions)));
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            return InvoiceErrors.EmailDeliveryFailed;
        }

        if (!Lifecycle.IsDraft(invoice.Status) || !Lifecycle.CanTransition(invoice.Status, nameof(Status.Sent)))
            return detail;
        invoice.Status = nameof(Status.Sent);
        invoice.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await database.Invoices.ReplaceOneAsync(
            entity => entity.Id == invoice.Id, invoice, cancellationToken: cancellationToken);
        return Presentation.ToDetail(invoice, sessions, userTimeZone);
    }

    public async Task<Result<Detail>> UpdateStatusAsync(
        string id,
        UpdateRequest request,
        string userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(userTimeZoneId);
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        if (!Lifecycle.TryNormalize(request.Status, out var status))
            return ValidationError.FromErrors((InvoiceErrors.StatusField, InvoiceErrors.StatusInvalid));
        var invoice = loaded.Value.Invoice;
        var normalizedStatus = status.ToString();
        if (!Lifecycle.CanTransition(invoice.Status, normalizedStatus))
            return InvoiceErrors.StatusTransitionNotAllowed(invoice.Status, normalizedStatus);
        invoice.Status = normalizedStatus;
        invoice.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await database.Invoices.ReplaceOneAsync(
            entity => entity.Id == invoice.Id, invoice, cancellationToken: cancellationToken);
        return await BuildDetailAsync(loaded.Value, userTimeZone);
    }

    private async Task<Result<LoadedInvoice>> LoadAsync(string id, CancellationToken cancellationToken)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var invoiceId))
            return InvoiceErrors.InvalidId;
        var organizationId = context.Value.OrganizationId;
        var invoice = await database.Invoices
            .Find(entity => entity.Id == invoiceId && entity.OrganizationId == organizationId)
            .FirstOrDefaultAsync(cancellationToken);
        return invoice is null
            ? InvoiceErrors.NotFound(id)
            : new LoadedInvoice(organizationId, invoiceId, invoice);
    }

    private async Task<Detail> BuildDetailAsync(LoadedInvoice loaded, TimeZoneInfo userTimeZone)
    {
        var sessions = await assignment.LoadBilledSessionsAsync(loaded.OrganizationId, loaded.InvoiceId);
        return Presentation.ToDetail(loaded.Invoice, sessions, userTimeZone);
    }

    private async Task<Result<IReadOnlyList<SelectableSession>>> BuildSelectableSessionsAsync(
        ObjectId organizationId,
        ObjectId contractId,
        ObjectId? invoiceId,
        CancellationToken cancellationToken)
    {
        var contract = await database.BillingContracts
            .Find(entity => entity.Id == contractId && entity.OrganizationId == organizationId)
            .FirstOrDefaultAsync(cancellationToken);
        if (contract is null)
            return InvoiceErrors.ContractNotFound;
        var project = await database.Projects
            .Find(entity => entity.Id == contract.ProjectId && entity.OrganizationId == organizationId)
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return InvoiceErrors.ProjectNotFound;
        var sessions = await assignment.LoadBillableSessionsAsync(organizationId, contractId, invoiceId);
        IReadOnlyList<SelectableSession> selectable = sessions
            .Select(session => Presentation.ToSelectableSession(session, project, contract))
            .ToList();
        return Result.Success(selectable);
    }

    private static Result<ListQuery> ValidateQuery(string? clientId, string? status, int take)
    {
        var errors = new List<(string Field, string Message)>();
        if (take is < InvoiceErrors.MinimumTake or > InvoiceErrors.MaximumTake)
            errors.Add((InvoiceErrors.TakeField, InvoiceErrors.TakeOutOfRange));
        ObjectId? parsedClientId = null;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            if (ObjectId.TryParse(clientId, out var value))
                parsedClientId = value;
            else
                errors.Add((InvoiceErrors.ClientIdField, InvoiceErrors.ClientIdInvalid));
        }

        string? normalizedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Lifecycle.TryNormalize(status, out var value))
                normalizedStatus = value.ToString();
            else
                errors.Add((InvoiceErrors.StatusField, InvoiceErrors.StatusInvalid));
        }

        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);
        return new ListQuery(parsedClientId, normalizedStatus);
    }

    private static async Task<TResult> AbortAsync<TResult>(
        IClientSessionHandle mongoSession,
        TResult result,
        CancellationToken cancellationToken)
    {
        if (mongoSession.IsInTransaction)
            await mongoSession.AbortTransactionAsync(cancellationToken);
        return result;
    }

    private sealed record LoadedInvoice(ObjectId OrganizationId, ObjectId InvoiceId, InvoiceEntity Invoice);

    private sealed record ListQuery(ObjectId? ClientId, string? Status);
}