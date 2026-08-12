namespace SartainStudios.Schema.Invoice;

public sealed record Summary(
    string Id,
    string OrganizationId,
    string ClientId,
    string InvoiceNumber,
    string ClientCompanyName,
    string ProjectName,
    DateTime DueDate,
    decimal TotalAmount,
    int TotalMinutesWorked,
    int TotalDaysWorked,
    decimal AverageRevenuePerDay,
    string Status,
    IReadOnlyList<string> BilledSessionIds,
    DateTime CreatedAt,
    DateTime UpdatedAt);