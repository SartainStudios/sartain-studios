using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Validation;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Project;
using ClientEntity = SartainStudios.Schema.DatabaseEntity.Client;
using CreateRequest = SartainStudios.Schema.Project.CreateRequest;
using ProjectEntity = SartainStudios.Schema.DatabaseEntity.Project;
using Status = SartainStudios.Schema.Project.Status;
using Summary = SartainStudios.Schema.Project.Summary;
using UpdateRequest = SartainStudios.Schema.Project.UpdateRequest;

namespace SartainStudios.Api.Service.Project;

public sealed class ProjectService(
    Database database,
    CurrentTenant currentTenant,
    Lookup lookup,
    TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<Summary>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        var projects = await database.Projects
            .Find<ProjectEntity>(project => project.OrganizationId == organizationId)
            .SortBy(project => project.Name)
            .ToListAsync(cancellationToken);
        if (projects.Count == 0)
            return Result.Success<IReadOnlyList<Summary>>([]);
        var clients = await lookup.ClientsAsync(organizationId, projects.Select(project => project.ClientId));
        IReadOnlyList<Summary> summaries = projects
            .Select(project => ToSummary(project, clients.GetValueOrDefault(project.ClientId)))
            .ToList();
        return Result.Success(summaries);
    }

    public async Task<Result<Summary>> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var projectId))
            return ProjectErrors.InvalidId;
        var project = await FindAsync(projectId, organizationId, cancellationToken);
        if (project is null)
            return ProjectErrors.NotFound(id);
        var client = await FindClientAsync(project.ClientId, organizationId, cancellationToken);
        return ToSummary(project, client);
    }

    public async Task<Result<Summary>> CreateAsync(
        CreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        var validation = Validate(request.ClientId, request.Name, request.Description, request.Status);
        if (validation.IsFailure)
            return validation.Error;
        var details = validation.Value;
        var client = await FindClientAsync(details.ClientId, organizationId, cancellationToken);
        if (client is null)
            return ProjectErrors.ClientNotFound;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var project = new ProjectEntity
        {
            OrganizationId = organizationId,
            ClientId = client.Id,
            Name = details.Name,
            Description = details.Description,
            Status = details.Status,
            CreatedAt = now,
            UpdatedAt = now
        };
        await database.Projects.InsertOneAsync(project, cancellationToken: cancellationToken);
        return ToSummary(project, client);
    }

    public async Task<Result<Summary>> UpdateAsync(
        string id,
        UpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var projectId))
            return ProjectErrors.InvalidId;
        var validation = Validate(request.ClientId, request.Name, request.Description, request.Status);
        if (validation.IsFailure)
            return validation.Error;
        var details = validation.Value;
        var project = await FindAsync(projectId, organizationId, cancellationToken);
        if (project is null)
            return ProjectErrors.NotFound(id);
        var client = await FindClientAsync(details.ClientId, organizationId, cancellationToken);
        if (client is null)
            return ProjectErrors.ClientNotFound;
        project.ClientId = client.Id;
        project.Name = details.Name;
        project.Description = details.Description;
        project.Status = details.Status;
        project.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await database.Projects.ReplaceOneAsync<ProjectEntity>(
            existing => existing.Id == project.Id, project, cancellationToken: cancellationToken);
        return ToSummary(project, client);
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var projectId))
            return ProjectErrors.InvalidId;
        var project = await FindAsync(projectId, organizationId, cancellationToken);
        if (project is null)
            return ProjectErrors.NotFound(id);
        var hasContracts = await database.BillingContracts
            .Find(contract => contract.OrganizationId == organizationId && contract.ProjectId == projectId)
            .AnyAsync(cancellationToken);
        if (hasContracts)
            return ProjectErrors.HasBillingContracts;
        var hasWorkSessions = await database.TimeSessions
            .Find(session => session.OrganizationId == organizationId && session.ProjectId == projectId)
            .AnyAsync(cancellationToken);
        if (hasWorkSessions)
            return ProjectErrors.HasWorkSessions;
        await database.Projects.DeleteOneAsync(existing => existing.Id == project.Id, cancellationToken);
        return Result.Success();
    }

    private Task<ProjectEntity?> FindAsync(
        ObjectId projectId,
        ObjectId organizationId,
        CancellationToken cancellationToken)
    {
        return database.Projects
            .Find<ProjectEntity>(project => project.Id == projectId && project.OrganizationId == organizationId)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private Task<ClientEntity?> FindClientAsync(
        ObjectId clientId,
        ObjectId organizationId,
        CancellationToken cancellationToken)
    {
        return database.Clients
            .Find(client => client.Id == clientId && client.OrganizationId == organizationId)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private static Result<ProjectDetails> Validate(
        string? clientId,
        string? name,
        string? description,
        string? status)
    {
        var errors = new List<(string Field, string Message)>();
        ObjectId parsedClientId = default;
        if (string.IsNullOrWhiteSpace(clientId))
            errors.Add((ProjectErrors.ClientIdField, ProjectErrors.ClientIdRequired));
        else if (!ObjectId.TryParse(clientId, out parsedClientId))
            errors.Add((ProjectErrors.ClientIdField, ProjectErrors.InvalidClientId.Description));
        if (string.IsNullOrWhiteSpace(name))
            errors.Add((ProjectErrors.NameField, ProjectErrors.NameRequired));
        if (string.IsNullOrWhiteSpace(description))
            errors.Add((ProjectErrors.DescriptionField, ProjectErrors.DescriptionRequired));
        if (!EnumName.TryNormalize<Status>(status, out var normalizedStatus))
            errors.Add((ProjectErrors.StatusField, ProjectErrors.StatusInvalid(EnumName.Options<Status>())));
        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);
        return new ProjectDetails(parsedClientId, name!.Trim(), description!.Trim(), normalizedStatus);
    }

    private static Summary ToSummary(ProjectEntity project, ClientEntity? client)
    {
        return new Summary(
            project.Id.ToString(),
            project.OrganizationId.ToString(),
            project.ClientId.ToString(),
            client?.CompanyName ?? string.Empty,
            project.Name,
            project.Description,
            project.Status,
            project.CreatedAt,
            project.UpdatedAt);
    }

    private sealed record ProjectDetails(ObjectId ClientId, string Name, string Description, string Status);
}