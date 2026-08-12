namespace SartainStudios.Schema.Invoice;

public sealed record DailyBreakdownEntry(
    DateOnly Date,
    int MinutesWorked,
    decimal Amount);