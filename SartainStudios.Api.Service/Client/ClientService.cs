using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Validation;
using SartainStudios.Schema;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Client;
using ClientEntity = SartainStudios.Schema.DatabaseEntity.Client;
using CreateRequest = SartainStudios.Schema.Client.CreateRequest;
using Summary = SartainStudios.Schema.Client.Summary;
using UpdateRequest = SartainStudios.Schema.Client.UpdateRequest;

namespace SartainStudios.Api.Service.Client;

public sealed class ClientService(Database database, CurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<Summary>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        var clients = await database.Clients
            .Find<ClientEntity>(client => client.OrganizationId == organizationId)
            .SortBy(client => client.CompanyName)
            .ThenBy(client => client.ContactPerson)
            .ToListAsync(cancellationToken);
        IReadOnlyList<Summary> summaries = clients.Select(ToSummary).ToList();
        return Result.Success(summaries);
    }

    public async Task<Result<Summary>> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var clientId))
            return ClientErrors.InvalidId;
        var client = await FindAsync(clientId, organizationId, cancellationToken);
        return client is null
            ? ClientErrors.NotFound(id)
            : ToSummary(client);
    }

    public async Task<Result<Summary>> CreateAsync(
        CreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        var validation = Validate(
            request.CompanyName, request.ContactPerson, request.Address, request.Email, request.PhoneNumber);
        if (validation.IsFailure)
            return validation.Error;
        var details = validation.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var client = new ClientEntity
        {
            OrganizationId = organizationId,
            CompanyName = details.CompanyName,
            ContactPerson = details.ContactPerson,
            Address = details.Address,
            Email = details.Email,
            PhoneNumber = details.PhoneNumber,
            CreatedAt = now,
            UpdatedAt = now
        };
        await database.Clients.InsertOneAsync(client, cancellationToken: cancellationToken);
        return ToSummary(client);
    }

    public async Task<Result<Summary>> UpdateAsync(
        string id,
        UpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var clientId))
            return ClientErrors.InvalidId;
        var validation = Validate(
            request.CompanyName, request.ContactPerson, request.Address, request.Email, request.PhoneNumber);
        if (validation.IsFailure)
            return validation.Error;
        var client = await FindAsync(clientId, organizationId, cancellationToken);
        if (client is null)
            return ClientErrors.NotFound(id);
        var details = validation.Value;
        client.CompanyName = details.CompanyName;
        client.ContactPerson = details.ContactPerson;
        client.Address = details.Address;
        client.Email = details.Email;
        client.PhoneNumber = details.PhoneNumber;
        client.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await database.Clients.ReplaceOneAsync<ClientEntity>(
            existing => existing.Id == client.Id, client, cancellationToken: cancellationToken);
        return ToSummary(client);
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var clientId))
            return ClientErrors.InvalidId;
        var client = await FindAsync(clientId, organizationId, cancellationToken);
        if (client is null)
            return ClientErrors.NotFound(id);
        var hasProjects = await database.Projects
            .Find(project => project.OrganizationId == organizationId && project.ClientId == clientId)
            .AnyAsync(cancellationToken);
        if (hasProjects)
            return ClientErrors.HasProjects;
        await database.Clients.DeleteOneAsync(existing => existing.Id == client.Id, cancellationToken);
        return Result.Success();
    }

    private Task<ClientEntity?> FindAsync(
        ObjectId clientId,
        ObjectId organizationId,
        CancellationToken cancellationToken)
    {
        return database.Clients
            .Find<ClientEntity>(client => client.Id == clientId && client.OrganizationId == organizationId)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private static Result<ClientDetails> Validate(
        string? companyName,
        string? contactPerson,
        Address? address,
        string? email,
        string? phoneNumber)
    {
        var errors = new List<(string Field, string Message)>();
        if (string.IsNullOrWhiteSpace(companyName))
            errors.Add((ClientErrors.CompanyNameField, ClientErrors.CompanyNameRequired));
        if (string.IsNullOrWhiteSpace(contactPerson))
            errors.Add((ClientErrors.ContactPersonField, ClientErrors.ContactPersonRequired));
        if (address is null)
            errors.Add((ClientErrors.AddressField, ClientErrors.AddressRequired));
        else
            ValidateAddress(address, errors);
        var trimmedEmail = email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedEmail))
            errors.Add((ClientErrors.EmailField, ClientErrors.EmailRequired));
        else if (!Contact.IsValidEmail(trimmedEmail))
            errors.Add((ClientErrors.EmailField, ClientErrors.EmailInvalid));
        var trimmedPhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedPhoneNumber))
            errors.Add((ClientErrors.PhoneNumberField, ClientErrors.PhoneNumberRequired));
        else if (!Contact.IsValidPhoneNumber(trimmedPhoneNumber))
            errors.Add((ClientErrors.PhoneNumberField, ClientErrors.PhoneNumberInvalid));
        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);
        return new ClientDetails(
            companyName!.Trim(),
            contactPerson!.Trim(),
            address!.Trimmed(),
            trimmedEmail,
            trimmedPhoneNumber);
    }

    private static void ValidateAddress(Address address, List<(string Field, string Message)> errors)
    {
        if (string.IsNullOrWhiteSpace(address.Line1))
            errors.Add((ClientErrors.AddressLine1Field, ClientErrors.AddressLine1Required));
        if (string.IsNullOrWhiteSpace(address.City))
            errors.Add((ClientErrors.AddressCityField, ClientErrors.AddressCityRequired));
        if (string.IsNullOrWhiteSpace(address.StateOrProvince))
            errors.Add((ClientErrors.AddressStateOrProvinceField, ClientErrors.AddressStateOrProvinceRequired));
        if (string.IsNullOrWhiteSpace(address.PostalCode))
            errors.Add((ClientErrors.AddressPostalCodeField, ClientErrors.AddressPostalCodeRequired));
        if (string.IsNullOrWhiteSpace(address.Country))
            errors.Add((ClientErrors.AddressCountryField, ClientErrors.AddressCountryRequired));
    }

    private static Summary ToSummary(ClientEntity client)
    {
        return new Summary(
            client.Id.ToString(),
            client.OrganizationId.ToString(),
            client.CompanyName,
            client.ContactPerson,
            client.Address,
            client.Email,
            client.PhoneNumber,
            client.CreatedAt,
            client.UpdatedAt);
    }

    private sealed record ClientDetails(
        string CompanyName,
        string ContactPerson,
        Address Address,
        string Email,
        string PhoneNumber);
}