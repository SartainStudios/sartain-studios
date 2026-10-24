using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Gig;

public static class GigReportBuilder
{
    public static Report Build(
        IEnumerable<GigSession> sessions,
        GigRateContext rate,
        TimeZoneInfo timeZone,
        DateTime nowUtc,
        int weeks)
    {
        var today = GigCalculator.LocalDate(nowUtc, timeZone);
        var thisWeekStart = GigCalculator.WeekStart(today);
        var weekCount = Math.Max(weeks, 2);
        var firstWeekStart = thisWeekStart.AddDays(-7 * (weekCount - 1));
        var displayStart = thisWeekStart.AddDays(-7 * (weeks - 1));
        var rows = sessions
            .Where(session => session.EndTime is not null)
            .Select(session => ToRow(session, rate, timeZone))
            .Where(row => row.Date >= firstWeekStart && row.Date <= today)
            .ToList();
        var summaries = Enumerable.Range(0, weekCount)
            .Select(index => firstWeekStart.AddDays(7 * index))
            .Select(weekStart => Summarize(
                weekStart,
                rows.Where(row => GigCalculator.WeekStart(row.Date) == weekStart).ToList(),
                rate))
            .ToList();
        var displayRows = rows.Where(row => row.Date >= displayStart).ToList();
        return new Report(
            rate.MileageDeductionEnabled,
            summaries[^1],
            summaries[^2],
            summaries.Where(summary => summary.WeekStart >= displayStart).ToList(),
            BuildDaily(displayRows, displayStart, today),
            BuildWeekdayAverages(displayRows));
    }

    public static WeekSummary Summarize(DateOnly weekStart, IReadOnlyCollection<Row> rows, GigRateContext rate)
    {
        var minutes = rows.Sum(row => row.Minutes);
        var gross = rows.Sum(row => row.Gross);
        var expenses = rows.Sum(row => row.Expenses);
        var profit = gross - expenses;
        var deduction = rate.MileageDeductionEnabled ? rows.Sum(row => row.Deduction ?? 0m) : (decimal?)null;
        return new WeekSummary(
            weekStart,
            rows.Count,
            minutes,
            gross,
            expenses,
            rows.Sum(row => row.Miles),
            profit,
            HourlyRate(gross, minutes),
            HourlyRate(profit, minutes),
            deduction,
            deduction is null ? null : profit - deduction);
    }

    private static IReadOnlyList<DailyTotal> BuildDaily(IReadOnlyList<Row> rows, DateOnly start, DateOnly end)
    {
        var byDate = rows.GroupBy(row => row.Date).ToDictionary(group => group.Key);
        var daily = new List<DailyTotal>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (!byDate.TryGetValue(date, out var group))
            {
                daily.Add(new DailyTotal(date, 0, 0m, 0m, 0m));
                continue;
            }

            var gross = group.Sum(row => row.Gross);
            var expenses = group.Sum(row => row.Expenses);
            daily.Add(new DailyTotal(date, group.Sum(row => row.Minutes), gross, expenses, gross - expenses));
        }

        return daily;
    }

    private static IReadOnlyList<WeekdayAverage> BuildWeekdayAverages(IReadOnlyList<Row> rows)
    {
        DayOfWeek[] order =
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        ];
        return order.Select(day =>
            {
                var dayRows = rows.Where(row => row.Date.DayOfWeek == day).ToList();
                var daysWorked = dayRows.Select(row => row.Date).Distinct().Count();
                if (daysWorked == 0) return new WeekdayAverage(day, 0, 0m, 0m, null);
                var gross = dayRows.Sum(row => row.Gross);
                var minutes = dayRows.Sum(row => row.Minutes);
                return new WeekdayAverage(
                    day,
                    daysWorked,
                    GigCalculator.Round(gross / daysWorked),
                    GigCalculator.Round((decimal)minutes / daysWorked),
                    HourlyRate(gross, minutes));
            })
            .ToList();
    }

    private static decimal? HourlyRate(decimal amount, int minutes)
    {
        return minutes <= 0 ? null : GigCalculator.Round(amount / (minutes / 60m));
    }

    private static Row ToRow(GigSession session, GigRateContext rate, TimeZoneInfo timeZone)
    {
        return new Row(
            GigCalculator.LocalDate(session.StartTime, timeZone),
            GigCalculator.ElapsedMinutes(session.StartTime, session.EndTime, session.EndTime ?? session.StartTime),
            GigCalculator.Gross(session.Earnings),
            GigCalculator.Expenses(session.Expenses),
            GigCalculator.Miles(session.StartOdometer, session.EndOdometer) ?? 0m,
            GigCalculator.MileageDeduction(session, rate, timeZone));
    }

    public sealed record Row(
        DateOnly Date,
        int Minutes,
        decimal Gross,
        decimal Expenses,
        decimal Miles,
        decimal? Deduction);
}