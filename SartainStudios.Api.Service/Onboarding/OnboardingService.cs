using System.Linq.Expressions;
using MongoDB.Driver;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Api;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;
using Status = SartainStudios.Schema.Onboarding.Status;

namespace SartainStudios.Api.Service.Onboarding;

public sealed class OnboardingService(Database database, CurrentTenant currentTenant)
{
    public async Task<Result<Status>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;

        var organizationTask = database.Organizations
            .Find<OrganizationEntity>(organization => organization.Id == organizationId)
            .FirstOrDefaultAsync(cancellationToken);
        var hasClientTask = ExistsAsync(database.Clients, client => client.OrganizationId == organizationId,
            cancellationToken);
        var hasProjectTask = ExistsAsync(database.Projects, project => project.OrganizationId == organizationId,
            cancellationToken);
        var hasBillingContractTask = ExistsAsync(database.BillingContracts,
            contract => contract.OrganizationId == organizationId, cancellationToken);
        var hasLoggedSessionTask = ExistsAsync(database.TimeSessions,
            session => session.OrganizationId == organizationId, cancellationToken);
        var hasInvoiceTask = ExistsAsync(database.Invoices, invoice => invoice.OrganizationId == organizationId,
            cancellationToken);

        await Task.WhenAll(
            organizationTask,
            hasClientTask,
            hasProjectTask,
            hasBillingContractTask,
            hasLoggedSessionTask,
            hasInvoiceTask);

        return new Status(
            IsCustomized(await organizationTask),
            await hasClientTask,
            await hasProjectTask,
            await hasBillingContractTask,
            await hasLoggedSessionTask,
            await hasInvoiceTask);
    }

    private static Task<bool> ExistsAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        Expression<Func<TDocument, bool>> filter,
        CancellationToken cancellationToken)
    {
        return collection.Find(filter).AnyAsync(cancellationToken);
    }

    private static bool IsCustomized(OrganizationEntity? organization)
    {
        if (organization is null)
            return false;

        var hasAddress = organization.Address?.HasValue ?? false;
        var hasPhoneNumber = !string.IsNullOrWhiteSpace(organization.PhoneNumber);

        return hasAddress && hasPhoneNumber;
    }
}