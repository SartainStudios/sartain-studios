namespace SartainStudios.Api.Schema.Invoice;

public sealed record InvoiceTotals(
    int TotalMinutesWorked,
    int TotalDaysWorked,
    decimal TotalAmount,
    decimal AverageRevenuePerDay);