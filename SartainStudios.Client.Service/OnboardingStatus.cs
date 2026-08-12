using SartainStudios.Client.Schema;
using SartainStudios.Client.Service.Authentication;
using BillingContractService = SartainStudios.Client.Service.BillingContract;
using ClientService = SartainStudios.Client.Service.Client;
using InvoiceService = SartainStudios.Client.Service.Invoice;
using OrganizationService = SartainStudios.Client.Service.Organization;
using ProjectService = SartainStudios.Client.Service.Project;
using WorkSessionService = SartainStudios.Client.Service.WorkSession;

namespace SartainStudios.Client.Service;

public sealed class OnboardingStatus(
    OrganizationService organizationService,
    ClientService clientService,
    ProjectService projectService,
    BillingContractService billingContractService,
    WorkSessionService workSessionService,
    InvoiceService invoiceService,
    TokenStore tokenStore)
{
    public async Task<OnboardingStatusResult> GetAsync()
    {
        var session = await tokenStore.LoadAsync();
        var organizationCustomizedTask = GetOrganizationCustomizedAsync(session?.OrganizationId);
        var clientsTask = clientService.ListAsync();
        var projectsTask = projectService.ListAsync();
        var billingContractsTask = billingContractService.ListAsync();
        var sessionsTask = workSessionService.ListAsync(take: 1);
        var invoicesTask = invoiceService.ListAsync();
        await Task.WhenAll(
            organizationCustomizedTask,
            clientsTask,
            projectsTask,
            billingContractsTask,
            sessionsTask,
            invoicesTask);
        return new OnboardingStatusResult(
            await organizationCustomizedTask,
            (await clientsTask).Count > 0,
            (await projectsTask).Count > 0,
            (await billingContractsTask).Count > 0,
            (await sessionsTask).Count > 0,
            (await invoicesTask).Count > 0);
    }

    private async Task<bool> GetOrganizationCustomizedAsync(string? organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId)) return false;
        try
        {
            var organization = await organizationService.GetAsync(organizationId);
            var hasAddress = organization.Address?.HasValue ?? false;
            var hasPhoneNumber = !string.IsNullOrWhiteSpace(organization.PhoneNumber);
            return hasAddress && hasPhoneNumber;
        }
        catch
        {
            return false;
        }
    }
}