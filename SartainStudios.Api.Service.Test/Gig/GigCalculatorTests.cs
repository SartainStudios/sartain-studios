using MongoDB.Bson;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Test.Gig;

public sealed class GigCalculatorTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Miles_ReturnsDifferenceWhenBothReadingsExist()
    {
        Assert.Equal(12.5m, GigCalculator.Miles(100m, 112.5m));
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(10, null)]
    [InlineData(20, 10)]
    public void Miles_ReturnsNullWhenReadingsAreMissingOrBackwards(int? start, int? end)
    {
        Assert.Null(GigCalculator.Miles(start, end));
    }

    [Theory]
    [InlineData("2026-08-10", "2026-08-10")]
    [InlineData("2026-08-12", "2026-08-10")]
    [InlineData("2026-08-16", "2026-08-10")]
    [InlineData("2026-08-17", "2026-08-17")]
    public void WeekStart_AlwaysReturnsTheMonday(string date, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), GigCalculator.WeekStart(DateOnly.Parse(date)));
    }

    [Fact]
    public void MileageDeduction_IsNullWhenSettingIsDisabled()
    {
        var session = NewSession("Car", 0m, 100m);

        Assert.Null(GigCalculator.MileageDeduction(session, GigRateContext.Disabled, TimeZoneInfo.Utc));
    }

    [Fact]
    public void MileageDeduction_UsesTheRateOfTheSessionsDate()
    {
        var session = NewSession("Car", 0m, 100m);
        session.StartTime = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        session.EndTime = new DateTime(2024, 6, 1, 16, 0, 0, DateTimeKind.Utc);

        var deduction = GigCalculator.MileageDeduction(session, new GigRateContext(true, null), TimeZoneInfo.Utc);

        Assert.Equal(67m, deduction);
    }

    [Fact]
    public void MileageDeduction_PrefersTheUserOverrideRate()
    {
        var session = NewSession("Car", 0m, 100m);

        var deduction = GigCalculator.MileageDeduction(session, new GigRateContext(true, 0.5m), TimeZoneInfo.Utc);

        Assert.Equal(50m, deduction);
    }

    [Theory]
    [InlineData("Bicycle")]
    [InlineData("EBike")]
    [InlineData("Other")]
    public void MileageDeduction_IsNullForVehiclesTheIrsRateDoesNotCover(string vehicleType)
    {
        var session = NewSession(vehicleType, 0m, 100m);

        Assert.Null(GigCalculator.MileageDeduction(session, new GigRateContext(true, null), TimeZoneInfo.Utc));
    }

    [Fact]
    public void ToSession_HidesDeductionWhenDisabledAndShowsBothProfitsWhenEnabled()
    {
        var session = NewSession("Car", 0m, 100m);
        var source = ObjectId.GenerateNewId();
        session.Earnings = [new GigEarningEntry { SourceId = source, Amount = 90m }];
        session.Expenses = [new GigExpenseEntry { CategoryId = ObjectId.GenerateNewId(), Amount = 10m }];

        var hidden = GigCalculator.ToSession(session, GigRateContext.Disabled, TimeZoneInfo.Utc, Now);
        var shown = GigCalculator.ToSession(session, new GigRateContext(true, 0.5m), TimeZoneInfo.Utc, Now);

        Assert.Equal(80m, hidden.Profit);
        Assert.Null(hidden.MileageDeduction);
        Assert.Null(hidden.ProfitAfterDeduction);
        Assert.Equal(80m, shown.Profit);
        Assert.Equal(50m, shown.MileageDeduction);
        Assert.Equal(30m, shown.ProfitAfterDeduction);
        Assert.Equal(100m, shown.Miles);
        Assert.Equal(240, shown.Minutes);
    }

    [Fact]
    public void ToSession_ASessionWithNoEntriesMeansNoIncome()
    {
        var session = NewSession("Car", null, null);

        var result = GigCalculator.ToSession(session, GigRateContext.Disabled, TimeZoneInfo.Utc, Now);

        Assert.Empty(result.Earnings);
        Assert.Equal(0m, result.GrossEarnings);
        Assert.Equal(0m, result.Profit);
    }

    [Fact]
    public void ToSession_OpenSessionCountsElapsedMinutesUpToNow()
    {
        var session = new GigSession { UserId = ObjectId.GenerateNewId(), StartTime = Now.AddMinutes(-90) };

        var result = GigCalculator.ToSession(session, GigRateContext.Disabled, TimeZoneInfo.Utc, Now);

        Assert.True(result.IsOpen);
        Assert.Equal(90, result.Minutes);
    }

    [Fact]
    public void MileageRates_FallsBackToTheNearestEarlierYear()
    {
        Assert.Equal(MileageRates.ForYear(2026), MileageRates.ForYear(2040));
        Assert.Equal(MileageRates.ForYear(2023), MileageRates.ForYear(2010));
        Assert.Equal(0.70m, MileageRates.ForYear(2025));
    }

    [Theory]
    [InlineData(2026, 1, 1, 0.725)]
    [InlineData(2026, 6, 30, 0.725)]
    [InlineData(2026, 7, 1, 0.76)]
    [InlineData(2026, 12, 31, 0.76)]
    public void MileageRates_2026RateChangesOnJuly1(int year, int month, int day, double expected)
    {
        Assert.Equal((decimal)expected, MileageRates.ForDate(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(2026, 6, 30, 72.5)]
    [InlineData(2026, 7, 1, 76)]
    public void MileageDeduction_UsesTheRateInEffectOnTheSessionDate(int year, int month, int day, double expected)
    {
        var session = NewSession("Car", 0m, 100m);
        session.StartTime = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);
        session.EndTime = new DateTime(year, month, day, 16, 0, 0, DateTimeKind.Utc);

        var deduction = GigCalculator.MileageDeduction(session, new GigRateContext(true, null), TimeZoneInfo.Utc);

        Assert.Equal((decimal)expected, deduction);
    }

    private static GigSession NewSession(string vehicleType, decimal? startOdometer, decimal? endOdometer)
    {
        return new GigSession
        {
            UserId = ObjectId.GenerateNewId(),
            StartTime = Now.AddHours(-4),
            EndTime = Now,
            StartOdometer = startOdometer,
            EndOdometer = endOdometer,
            VehicleType = vehicleType
        };
    }
}