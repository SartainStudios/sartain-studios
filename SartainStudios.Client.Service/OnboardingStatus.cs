using System.Net.Http.Json;
using SartainStudios.Client.Schema;
using SartainStudios.Client.Schema.Api;
using Status = SartainStudios.Schema.Onboarding.Status;

namespace SartainStudios.Client.Service;

public sealed class OnboardingStatus(HttpClient httpClient)
{
    public async Task<OnboardingStatusResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("api/onboarding", cancellationToken);
        await Response.EnsureSuccessAsync(response);
        var status = await response.Content.ReadFromJsonAsync<Status>(cancellationToken)
                     ?? throw new InvalidOperationException("Empty onboarding response.");
        return new OnboardingStatusResult(
            status.OrganizationCustomized,
            status.HasClient,
            status.HasProject,
            status.HasBillingContract,
            status.HasLoggedSession,
            status.HasInvoice);
    }
}