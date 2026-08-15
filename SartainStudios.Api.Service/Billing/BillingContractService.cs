using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Validation;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Billing;
using BillingContractEntity = SartainStudios.Schema.DatabaseEntity.BillingContract;
using CreateRequest = SartainStudios.Schema.Billing.CreateRequest;
using ProjectEntity = SartainStudios.Schema.DatabaseEntity.Project;
using Summary = SartainStudios.Schema.Billing.Summary;
using UpdateRequest = SartainStudios.Schema.Billing.UpdateRequest;

namespace SartainStudios.Api.Service.Billing;

public sealed class BillingContractService(
    Database database,
    CurrentTenant currentTenant,
    Lookup lookup,
    Sequence invoiceSequence,
    Draft draftInvoice,
    TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<Summary>>> ListAsync(
        string? projectId = null,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        var filter = Builders<BillingContractEntity>.Filter.Eq(contract => contract.OrganizationId, organizationId);
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            if (!ObjectId.TryParse(projectId, out var parsedProjectId))
                return BillingContractErrors.InvalidProjectId;
            filter &= Builders<BillingContractEntity>.Filter.Eq(contract => contract.ProjectId, parsedProjectId);
        }

        var contracts = await database.BillingContracts
            .Find(filter)
            .SortByDescending(contract => contract.IsActive)
            .ThenByDescending(contract => contract.UpdatedAt)
            .ToListAsync(cancellationToken);
        if (contracts.Count == 0)
            return Result.Success<IReadOnlyList<Summary>>([]);
        var projects = await lookup.ProjectsAsync(organizationId, contracts.Select(contract => contract.ProjectId));
        IReadOnlyList<Summary> summaries = contracts
            .Select(contract => ToSummary(contract, projects.GetValueOrDefault(contract.ProjectId)))
            .ToList();
        return Result.Success(summaries);
    }

    public async Task<Result<Summary>> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var contractId))
            return BillingContractErrors.InvalidId;
        var contract = await FindAsync(contractId, organizationId, cancellationToken);
        if (contract is null)
            return BillingContractErrors.NotFound(id);
        var project = await FindProjectAsync(contract.ProjectId, organizationId, cancellationToken);
        return ToSummary(contract, project);
    }

    public async Task<Result<Summary>> CreateAsync(
        CreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        var validation = Validate(
            request.ProjectId,
            request.HourlyRate,
            request.ExpectedMinutes,
            request.BillingCycle,
            request.ServiceProvided,
            request.InvoicePrefix);
        if (validation.IsFailure)
            return validation.Error;
        var details = validation.Value;
        var project = await FindProjectAsync(details.ProjectId, organizationId, cancellationToken);
        if (project is null)
            return BillingContractErrors.ProjectNotFound;
        if (request.IsActive &&
            await HasActiveContractAsync(organizationId, details.ProjectId, null, cancellationToken))
            return BillingContractErrors.ActiveContractExists;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var contract = new BillingContractEntity
        {
            OrganizationId = organizationId,
            ProjectId = details.ProjectId,
            HourlyRate = request.HourlyRate,
            ExpectedMinutes = request.ExpectedMinutes,
            BillingCycle = details.BillingCycle,
            ServiceProvided = details.ServiceProvided,
            InvoicePrefix = details.InvoicePrefix,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };
        await invoiceSequence.InitializeAsync(organizationId, contract.InvoicePrefix);
        try
        {
            await database.BillingContracts.InsertOneAsync(contract, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (IsActiveContractConflict(exception, request.IsActive))
        {
            return BillingContractErrors.ActiveContractExists;
        }

        return ToSummary(contract, project);
    }

    public async Task<Result<Summary>> UpdateAsync(
        string id,
        UpdateRequest request,
        string userTimeZone,
        CancellationToken cancellationToken = default)
    {
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(userTimeZone);

        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var contractId))
            return BillingContractErrors.InvalidId;
        var validation = Validate(
            request.ProjectId,
            request.HourlyRate,
            request.ExpectedMinutes,
            request.BillingCycle,
            request.ServiceProvided,
            request.InvoicePrefix);
        if (validation.IsFailure)
            return validation.Error;
        var details = validation.Value;
        var contract = await FindAsync(contractId, organizationId, cancellationToken);
        if (contract is null)
            return BillingContractErrors.NotFound(id);
        var project = await FindProjectAsync(details.ProjectId, organizationId, cancellationToken);
        if (project is null)
            return BillingContractErrors.ProjectNotFound;
        var isActivating = request.IsActive && (!contract.IsActive || contract.ProjectId != details.ProjectId);
        if (isActivating &&
            await HasActiveContractAsync(organizationId, details.ProjectId, contract.Id, cancellationToken))
            return BillingContractErrors.ActiveContractExists;
        contract.ProjectId = details.ProjectId;
        contract.HourlyRate = request.HourlyRate;
        contract.ExpectedMinutes = request.ExpectedMinutes;
        contract.BillingCycle = details.BillingCycle;
        contract.ServiceProvided = details.ServiceProvided;
        contract.InvoicePrefix = details.InvoicePrefix;
        contract.IsActive = request.IsActive;
        contract.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await invoiceSequence.InitializeAsync(organizationId, contract.InvoicePrefix);
        try
        {
            await database.BillingContracts.ReplaceOneAsync(
                existing => existing.Id == contract.Id, contract, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (IsActiveContractConflict(exception, request.IsActive))
        {
            return BillingContractErrors.ActiveContractExists;
        }

        await draftInvoice.RefreshForContractAsync(organizationId, contract, project, timeZoneInfo);

        return ToSummary(contract, project);
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var contractId))
            return BillingContractErrors.InvalidId;
        var contract = await FindAsync(contractId, organizationId, cancellationToken);
        if (contract is null)
            return BillingContractErrors.NotFound(id);
        var hasWorkSessions = await database.TimeSessions
            .Find(session => session.OrganizationId == organizationId && session.ContractId == contract.Id)
            .AnyAsync(cancellationToken);
        if (hasWorkSessions)
            return BillingContractErrors.HasWorkSessions;
        await database.BillingContracts.DeleteOneAsync(existing => existing.Id == contract.Id, cancellationToken);
        return Result.Success();
    }

    private Task<BillingContractEntity?> FindAsync(
        ObjectId contractId,
        ObjectId organizationId,
        CancellationToken cancellationToken)
    {
        return database.BillingContracts
            .Find(contract => contract.Id == contractId && contract.OrganizationId == organizationId)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private Task<ProjectEntity?> FindProjectAsync(
        ObjectId projectId,
        ObjectId organizationId,
        CancellationToken cancellationToken)
    {
        return database.Projects
            .Find(project => project.Id == projectId && project.OrganizationId == organizationId)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private Task<bool> HasActiveContractAsync(
        ObjectId organizationId,
        ObjectId projectId,
        ObjectId? excludedContractId,
        CancellationToken cancellationToken)
    {
        var filter = Builders<BillingContractEntity>.Filter.Eq(contract => contract.OrganizationId, organizationId)
                     & Builders<BillingContractEntity>.Filter.Eq(contract => contract.ProjectId, projectId)
                     & Builders<BillingContractEntity>.Filter.Eq(contract => contract.IsActive, true);
        if (excludedContractId is { } excluded)
            filter &= Builders<BillingContractEntity>.Filter.Ne(contract => contract.Id, excluded);
        return database.BillingContracts.Find(filter).AnyAsync(cancellationToken);
    }

    private static bool IsActiveContractConflict(MongoWriteException exception, bool isActive)
    {
        return isActive && exception.WriteError?.Category == ServerErrorCategory.DuplicateKey;
    }

    private static Result<ContractDetails> Validate(
        string? projectId,
        decimal hourlyRate,
        int expectedMinutes,
        string? billingCycle,
        string? serviceProvided,
        string? invoicePrefix)
    {
        var errors = new List<(string Field, string Message)>();
        if (string.IsNullOrWhiteSpace(projectId))
            errors.Add((BillingContractErrors.ProjectIdField, BillingContractErrors.ProjectIdRequired));
        else if (!ObjectId.TryParse(projectId, out _))
            errors.Add((BillingContractErrors.ProjectIdField, BillingContractErrors.InvalidProjectId.Description));
        if (hourlyRate <= 0)
            errors.Add((BillingContractErrors.HourlyRateField, BillingContractErrors.HourlyRateRequired));
        if (expectedMinutes <= 0)
            errors.Add((BillingContractErrors.ExpectedMinutesField, BillingContractErrors.ExpectedMinutesRequired));
        if (!EnumName.TryNormalize<Cycle>(billingCycle, out var normalizedBillingCycle))
            errors.Add((BillingContractErrors.BillingCycleField,
                BillingContractErrors.BillingCycleInvalid(EnumName.Options<Cycle>())));
        if (string.IsNullOrWhiteSpace(serviceProvided))
            errors.Add((BillingContractErrors.ServiceProvidedField, BillingContractErrors.ServiceProvidedRequired));
        var normalizedInvoicePrefix = invoicePrefix?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedInvoicePrefix))
            errors.Add((BillingContractErrors.InvoicePrefixField, BillingContractErrors.InvoicePrefixRequired));
        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);
        return new ContractDetails(
            ObjectId.Parse(projectId!),
            normalizedBillingCycle,
            serviceProvided!.Trim(),
            normalizedInvoicePrefix);
    }

    private static Summary ToSummary(BillingContractEntity contract, ProjectEntity? project)
    {
        return new Summary(
            contract.Id.ToString(),
            contract.OrganizationId.ToString(),
            contract.ProjectId.ToString(),
            project?.Name ?? string.Empty,
            contract.HourlyRate,
            contract.ExpectedMinutes,
            contract.BillingCycle,
            contract.ServiceProvided,
            contract.InvoicePrefix.Trim().ToUpperInvariant(),
            contract.IsActive,
            contract.CreatedAt,
            contract.UpdatedAt);
    }

    private sealed record ContractDetails(
        ObjectId ProjectId,
        string BillingCycle,
        string ServiceProvided,
        string InvoicePrefix);
}