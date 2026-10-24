using MongoDB.Bson;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Test.Gig;

public sealed class GigReportBuilderTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_SummarizesThisWeekAndLastWeekWithMondayStart()
    {
        var sessions = new[]
        {
            NewSession(new DateTime(2026, 8, 9, 17, 0, 0, DateTimeKind.Utc), 2, 40m, 5m),
            NewSession(new DateTime(2026, 8, 10, 17, 0, 0, DateTimeKind.Utc), 4, 100m, 20m, 30m),
            NewSession(new DateTime(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc), 2, 50m)
        };

        var report = GigReportBuilder.Build(sessions, GigRateContext.Disabled, TimeZoneInfo.Utc, Now, 4);

        Assert.Equal(new DateOnly(2026, 8, 10), report.ThisWeek.WeekStart);
        Assert.Equal(2, report.ThisWeek.SessionCount);
        Assert.Equal(360, report.ThisWeek.Minutes);
        Assert.Equal(150m, report.ThisWeek.Gross);
        Assert.Equal(20m, report.ThisWeek.Expenses);
        Assert.Equal(130m, report.ThisWeek.Profit);
        Assert.Equal(30m, report.ThisWeek.Miles);
        Assert.Equal(25m, report.ThisWeek.GrossHourlyRate);
        Assert.Equal(new DateOnly(2026, 8, 3), report.LastWeek.WeekStart);
        Assert.Equal(40m, report.LastWeek.Gross);
        Assert.Equal(35m, report.LastWeek.Profit);
    }

    [Fact]
    public void Build_UsesTheUsersTimeZoneToAssignSessionsToDays()
    {
        var pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

        var sessions = new[]
        {
            NewSession(new DateTime(2026, 8, 10, 2, 0, 0, DateTimeKind.Utc), 1, 30m)
        };

        var report = GigReportBuilder.Build(sessions, GigRateContext.Disabled, pacific, Now, 4);

        Assert.Equal(0m, report.ThisWeek.Gross);
        Assert.Equal(30m, report.LastWeek.Gross);
    }

    [Fact]
    public void Build_ReturnsEmptyWeeksAndZeroFilledDays()
    {
        var report = GigReportBuilder.Build([], GigRateContext.Disabled, TimeZoneInfo.Utc, Now, 3);

        Assert.Equal(3, report.Weeks.Count);
        Assert.All(report.Weeks, week => Assert.Equal(0m, week.Gross));
        Assert.All(report.Weeks, week => Assert.Null(week.GrossHourlyRate));
        Assert.Equal(7 * 2 + 3, report.Daily.Count);
        Assert.Equal(new DateOnly(2026, 8, 12), report.Daily[^1].Date);
        Assert.Equal(7, report.WeekdayAverages.Count);
        Assert.Equal(DayOfWeek.Monday, report.WeekdayAverages[0].Day);
    }

    [Fact]
    public void Build_IgnoresRunningSessions()
    {
        var running = new GigSession
        {
            UserId = ObjectId.GenerateNewId(),
            StartTime = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc)
        };

        var report = GigReportBuilder.Build([running], GigRateContext.Disabled, TimeZoneInfo.Utc, Now, 2);

        Assert.Equal(0, report.ThisWeek.SessionCount);
    }

    [Fact]
    public void Build_ComputesWeekdayAverages()
    {
        var sessions = new[]
        {
            NewSession(new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc), 2, 40m),
            NewSession(new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc), 2, 60m)
        };

        var report = GigReportBuilder.Build(sessions, GigRateContext.Disabled, TimeZoneInfo.Utc, Now, 4);

        var wednesday = report.WeekdayAverages.Single(day => day.Day == DayOfWeek.Wednesday);
        Assert.Equal(2, wednesday.DaysWorked);
        Assert.Equal(50m, wednesday.AverageGross);
        Assert.Equal(120m, wednesday.AverageMinutes);
        Assert.Equal(25m, wednesday.GrossHourlyRate);
    }

    [Fact]
    public void Build_ShowsDeductionOnlyWhenEnabled()
    {
        var sessions = new[]
        {
            NewSession(new DateTime(2026, 8, 11, 10, 0, 0, DateTimeKind.Utc), 2, 100m, 20m, 40m)
        };

        var off = GigReportBuilder.Build(sessions, GigRateContext.Disabled, TimeZoneInfo.Utc, Now, 2);
        var on = GigReportBuilder.Build(sessions, new GigRateContext(true, 0.5m), TimeZoneInfo.Utc, Now, 2);

        Assert.False(off.MileageDeductionEnabled);
        Assert.Null(off.ThisWeek.MileageDeduction);
        Assert.Null(off.ThisWeek.ProfitAfterDeduction);
        Assert.True(on.MileageDeductionEnabled);
        Assert.Equal(20m, on.ThisWeek.MileageDeduction);
        Assert.Equal(80m, on.ThisWeek.Profit);
        Assert.Equal(60m, on.ThisWeek.ProfitAfterDeduction);
    }

    private static GigSession NewSession(DateTime start, int hours, decimal gross, decimal expenses = 0m,
        decimal? miles = null)
    {
        return new GigSession
        {
            UserId = ObjectId.GenerateNewId(),
            StartTime = start,
            EndTime = start.AddHours(hours),
            VehicleType = nameof(VehicleType.Car),
            StartOdometer = miles is null ? null : 1000m,
            EndOdometer = miles is null ? null : 1000m + miles,
            Earnings = [new GigEarningEntry { SourceId = ObjectId.GenerateNewId(), Amount = gross }],
            Expenses = expenses > 0
                ? [new GigExpenseEntry { CategoryId = ObjectId.GenerateNewId(), Amount = expenses }]
                : []
        };
    }
}