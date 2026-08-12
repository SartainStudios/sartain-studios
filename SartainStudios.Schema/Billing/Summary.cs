namespace SartainStudios.Schema.Billing;

public sealed record Summary(
    string Id,
    string OrganizationId,
    string ProjectId,
    string ProjectName,
    decimal HourlyRate,
    int ExpectedMinutes,
    string BillingCycle,
    string ServiceProvided,
    string InvoicePrefix,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);