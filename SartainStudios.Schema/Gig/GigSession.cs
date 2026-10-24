namespace SartainStudios.Schema.Gig;

public sealed record EarningEntry(string SourceId, decimal Amount, string? Note);

public sealed record ExpenseEntry(string CategoryId, decimal Amount, string? Note);

public sealed record GigSessionDto(
    string Id,
    DateTime StartTime,
    DateTime? EndTime,
    bool IsOpen,
    int Minutes,
    decimal? StartOdometer,
    decimal? EndOdometer,
    decimal? Miles,
    string VehicleType,
    bool IsMileageDeductible,
    string Notes,
    IReadOnlyList<EarningEntry> Earnings,
    IReadOnlyList<ExpenseEntry> Expenses,
    decimal GrossEarnings,
    decimal TotalExpenses,
    decimal Profit,
    decimal? MileageDeduction,
    decimal? ProfitAfterDeduction);

public sealed record GigSessionState(GigSessionDto? CurrentSession, DateTime ServerTime);

public sealed record StartGigSessionRequest(DateTime? StartTime, decimal? StartOdometer, string? VehicleType);

public sealed record StopGigSessionRequest(
    DateTime? EndTime,
    decimal? EndOdometer,
    string? Notes,
    IReadOnlyList<EarningEntry>? Earnings,
    IReadOnlyList<ExpenseEntry>? Expenses,
    string? VehicleType = null,
    DateTime? StartTime = null,
    decimal? StartOdometer = null);

public sealed record GigSessionRequest(
    DateTime StartTime,
    DateTime? EndTime,
    decimal? StartOdometer,
    decimal? EndOdometer,
    string? VehicleType,
    string? Notes,
    IReadOnlyList<EarningEntry>? Earnings,
    IReadOnlyList<ExpenseEntry>? Expenses);