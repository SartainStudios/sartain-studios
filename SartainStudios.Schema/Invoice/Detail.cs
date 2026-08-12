using SartainStudios.Schema.Organization;

namespace SartainStudios.Schema.Invoice;

public sealed record Detail(
    string Id,
    string OrganizationId,
    string ClientId,
    string InvoiceNumber,
    Snapshot OrganizationSnapshot,
    Client.Snapshot ClientSnapshot,
    Project.Snapshot ProjectSnapshot,
    DateTime DueDate,
    decimal TotalAmount,
    int TotalMinutesWorked,
    int TotalDaysWorked,
    decimal AverageRevenuePerDay,
    string Status,
    IReadOnlyList<string> BilledSessionIds,
    IReadOnlyList<DailyBreakdownEntry> DailyBreakdown,
    DateTime CreatedAt,
    DateTime UpdatedAt);