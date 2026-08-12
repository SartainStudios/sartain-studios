namespace SartainStudios.Client.Schema;

public sealed record OnboardingStatusResult(
    bool OrganizationCustomized,
    bool HasClient,
    bool HasProject,
    bool HasBillingContract,
    bool HasLoggedSession,
    bool HasInvoice);