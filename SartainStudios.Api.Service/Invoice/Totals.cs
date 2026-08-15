using SartainStudios.Api.Schema.Invoice;
using SartainStudios.Schema.DatabaseEntity;
using DailyBreakdownEntry = SartainStudios.Schema.Invoice.DailyBreakdownEntry;

namespace SartainStudios.Api.Service.Invoice;

public static class Totals
{
    public static InvoiceTotals Calculate(
        IReadOnlyList<WorkSession> sessions,
        decimal hourlyRate,
        TimeZoneInfo userTimeZone)
    {
        var totalMinutesWorked = 0;
        var workedDays = new HashSet<DateOnly>();

        foreach (var session in sessions)
        {
            if (session.EndTime is null) continue;
            if (session.EndTime.Value < session.StartTime)
                throw new InvalidOperationException("A time session cannot end before it starts.");

            var minutesWorked = Math.Max(0, (int)Math.Floor((session.EndTime.Value - session.StartTime).TotalMinutes));
            totalMinutesWorked += minutesWorked;

            var localStartTime = TimeZoneInfo.ConvertTimeFromUtc(session.StartTime, userTimeZone);
            var localEndTime = TimeZoneInfo.ConvertTimeFromUtc(session.EndTime.Value, userTimeZone);

            var currentDate = DateOnly.FromDateTime(localStartTime);
            var endDate = DateOnly.FromDateTime(localEndTime);

            while (currentDate <= endDate)
            {
                workedDays.Add(currentDate);
                currentDate = currentDate.AddDays(1);
            }
        }

        var totalAmount = Math.Round(hourlyRate * totalMinutesWorked / 60m, 2, MidpointRounding.AwayFromZero);
        var totalDaysWorked = workedDays.Count;
        var averageRevenuePerDay = totalDaysWorked == 0
            ? 0m
            : Math.Round(totalAmount / totalDaysWorked, 2, MidpointRounding.AwayFromZero);

        return new InvoiceTotals(totalMinutesWorked, totalDaysWorked, totalAmount, averageRevenuePerDay);
    }

    public static IReadOnlyList<DailyBreakdownEntry> CalculateDailyBreakdown(
        IReadOnlyList<WorkSession> sessions,
        decimal hourlyRate,
        TimeZoneInfo userTimeZone)
    {
        var dailyMinutes = new Dictionary<DateOnly, int>();

        foreach (var session in sessions)
        {
            if (session.EndTime is null) continue;

            var localStartTime = TimeZoneInfo.ConvertTimeFromUtc(session.StartTime, userTimeZone);
            var localEndTime = TimeZoneInfo.ConvertTimeFromUtc(session.EndTime.Value, userTimeZone);

            var startDate = DateOnly.FromDateTime(localStartTime);
            var endDate = DateOnly.FromDateTime(localEndTime);
            var current = startDate;

            while (current <= endDate)
            {
                var localDayStart = current.ToDateTime(TimeOnly.MinValue);
                var localDayEnd = current.AddDays(1).ToDateTime(TimeOnly.MinValue);

                var utcDayStart = TimeZoneInfo.ConvertTimeToUtc(localDayStart, userTimeZone);
                var utcDayEnd = TimeZoneInfo.ConvertTimeToUtc(localDayEnd, userTimeZone);

                var clippedStart = session.StartTime < utcDayStart ? utcDayStart : session.StartTime;
                var clippedEnd = session.EndTime.Value > utcDayEnd ? utcDayEnd : session.EndTime.Value;

                if (clippedEnd > clippedStart)
                {
                    var minutes = (int)Math.Floor((clippedEnd - clippedStart).TotalMinutes);
                    dailyMinutes[current] = dailyMinutes.GetValueOrDefault(current) + minutes;
                }

                current = current.AddDays(1);
            }
        }

        return dailyMinutes
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new DailyBreakdownEntry(
                kvp.Key,
                kvp.Value,
                Math.Round(hourlyRate * kvp.Value / 60m, 2, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    public static bool HasOverlappingSessions(IReadOnlyList<WorkSession> sessions)
    {
        foreach (var userGroup in sessions.GroupBy(session => session.UserId))
        {
            DateTime? lastEnd = null;
            foreach (var session in userGroup.OrderBy(session => session.StartTime).ThenBy(session => session.EndTime))
            {
                if (session.EndTime is null)
                    continue;
                var endTime = session.EndTime.Value;
                if (lastEnd.HasValue && session.StartTime < lastEnd.Value)
                    return true;
                lastEnd = lastEnd.HasValue && lastEnd.Value > endTime ? lastEnd.Value : endTime;
            }
        }

        return false;
    }
}