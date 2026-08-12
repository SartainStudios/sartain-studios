namespace SartainStudios.Schema.Billing;

public sealed record UpdateRequest(
    string ProjectId,
    decimal HourlyRate,
    int ExpectedMinutes,
    string BillingCycle,
    string ServiceProvided,
    string InvoicePrefix,
    bool IsActive);