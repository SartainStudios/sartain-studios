namespace SartainStudios.Schema.Gig;

public static class GigReportRange
{
    public const int AllTime = 0;
}

public sealed record WeekSummary(
    DateOnly WeekStart,
    int SessionCount,
    int Minutes,
    decimal Gross,
    decimal Expenses,
    decimal Miles,
    decimal Profit,
    decimal? GrossHourlyRate,
    decimal? ProfitHourlyRate,
    decimal? MileageDeduction,
    decimal? ProfitAfterDeduction);

public sealed record DailyTotal(
    DateOnly Date,
    int Minutes,
    decimal Gross,
    decimal Expenses,
    decimal Profit);

public sealed record WeekdayAverage(
    DayOfWeek Day,
    int DaysWorked,
    decimal AverageGross,
    decimal AverageMinutes,
    decimal? GrossHourlyRate);

public sealed record AllTimeSummary(
    int SessionCount,
    int Minutes,
    decimal Gross,
    decimal Expenses,
    decimal Profit,
    decimal? GrossHourlyRate,
    decimal? ProfitHourlyRate);

public sealed record Report(
    bool MileageDeductionEnabled,
    AllTimeSummary AllTime,
    WeekSummary ThisWeek,
    WeekSummary LastWeek,
    IReadOnlyList<WeekSummary> Weeks,
    IReadOnlyList<DailyTotal> Daily,
    IReadOnlyList<WeekdayAverage> WeekdayAverages);