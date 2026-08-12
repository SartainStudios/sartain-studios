namespace SartainStudios.Schema.Billing;

public sealed record CreateRequest(
    string ProjectId,
    decimal HourlyRate,
    int ExpectedMinutes,
    string BillingCycle,
    string ServiceProvided,
    string InvoicePrefix,
    bool IsActive);