using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Gig;

public sealed record GigRateContext(bool MileageDeductionEnabled, decimal? RateOverride)
{
    public static readonly GigRateContext Disabled = new(false, null);

    public decimal RateForDate(DateOnly date)
    {
        return RateOverride ?? MileageRates.ForDate(date);
    }
}

public static class GigCalculator
{
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal? Miles(decimal? startOdometer, decimal? endOdometer)
    {
        if (startOdometer is not { } start || endOdometer is not { } end || end < start) return null;
        return end - start;
    }

    public static int ElapsedMinutes(DateTime startTime, DateTime? endTime, DateTime now)
    {
        var end = endTime ?? now;
        return Math.Max(0, (int)Math.Floor((end - startTime).TotalMinutes));
    }

    public static decimal Gross(IEnumerable<GigEarningEntry> earnings)
    {
        return earnings.Sum(entry => entry.Amount);
    }

    public static decimal Expenses(IEnumerable<GigExpenseEntry> expenses)
    {
        return expenses.Sum(entry => entry.Amount);
    }

    public static DateOnly LocalDate(DateTime utcValue, TimeZoneInfo timeZone)
    {
        var utc = DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }

    public static DateOnly WeekStart(DateOnly date)
    {
        return date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    }

    public static DateTime LocalMidnightToUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local)) local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    public static decimal? MileageDeduction(GigSession session, GigRateContext rate, TimeZoneInfo timeZone)
    {
        if (!rate.MileageDeductionEnabled || !VehicleTypes.IsDeductible(session.VehicleType)) return null;
        if (Miles(session.StartOdometer, session.EndOdometer) is not { } miles) return null;
        return Round(miles * rate.RateForDate(LocalDate(session.StartTime, timeZone)));
    }

    public static GigSessionDto ToSession(GigSession session, GigRateContext rate, TimeZoneInfo timeZone, DateTime now)
    {
        var gross = Gross(session.Earnings);
        var expenses = Expenses(session.Expenses);
        var profit = gross - expenses;
        var deduction = MileageDeduction(session, rate, timeZone);
        return new GigSessionDto(
            session.Id.ToString(),
            DateTime.SpecifyKind(session.StartTime, DateTimeKind.Utc),
            session.EndTime is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null,
            session.EndTime is null,
            ElapsedMinutes(session.StartTime, session.EndTime, now),
            session.StartOdometer,
            session.EndOdometer,
            Miles(session.StartOdometer, session.EndOdometer),
            session.VehicleType,
            VehicleTypes.IsDeductible(session.VehicleType),
            session.Notes,
            session.Earnings.Select(entry => new EarningEntry(entry.SourceId.ToString(), entry.Amount, entry.Note))
                .ToList(),
            session.Expenses.Select(entry => new ExpenseEntry(entry.CategoryId.ToString(), entry.Amount, entry.Note))
                .ToList(),
            gross,
            expenses,
            profit,
            deduction,
            deduction is null ? null : profit - deduction);
    }
}