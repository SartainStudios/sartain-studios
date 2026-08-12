namespace SartainStudios.Schema.Onboarding;

public sealed record Status(
    bool OrganizationCustomized,
    bool HasClient,
    bool HasProject,
    bool HasBillingContract,
    bool HasLoggedSession,
    bool HasInvoice);