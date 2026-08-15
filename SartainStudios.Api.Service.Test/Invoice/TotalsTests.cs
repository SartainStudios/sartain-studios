using MongoDB.Bson;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Schema.DatabaseEntity;

namespace SartainStudios.Api.Service.Test.Invoice;

public sealed class TotalsTests
{
    [Fact]
    public void Calculate_ComputesTotalsForSingleSession()
    {
        var sessions = new List<WorkSession>
        {
            new()
            {
                StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc)
            }
        };

        var totals = Totals.Calculate(sessions, 60m, TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        Assert.Equal(60, totals.TotalMinutesWorked);
        Assert.Equal(1, totals.TotalDaysWorked);
        Assert.Equal(60m, totals.TotalAmount);
        Assert.Equal(60m, totals.AverageRevenuePerDay);
    }

    [Fact]
    public void Calculate_ReturnsZeroTotalsWhenNoSessions()
    {
        var totals = Totals.Calculate([], 100m, TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        Assert.Equal(0, totals.TotalMinutesWorked);
        Assert.Equal(0, totals.TotalDaysWorked);
        Assert.Equal(0m, totals.TotalAmount);
        Assert.Equal(0m, totals.AverageRevenuePerDay);
    }

    [Fact]
    public void CalculateDailyBreakdown_ReturnsEntryForSessionDay()
    {
        var sessions = new List<WorkSession>
        {
            new()
            {
                StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc)
            }
        };

        var breakdown =
            Totals.CalculateDailyBreakdown(sessions, 60m, TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        var entry = Assert.Single(breakdown);
        Assert.Equal(new DateOnly(2024, 1, 1), entry.Date);
        Assert.Equal(60, entry.MinutesWorked);
        Assert.Equal(60m, entry.Amount);
    }

    [Fact]
    public void HasOverlappingSessions_ReturnsTrueWhenSessionsOverlap()
    {
        var userId = ObjectId.GenerateNewId();
        var sessions = new List<WorkSession>
        {
            new()
            {
                UserId = userId,
                StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                UserId = userId,
                StartTime = new DateTime(2024, 1, 1, 9, 30, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc)
            }
        };

        Assert.True(Totals.HasOverlappingSessions(sessions));
    }

    [Fact]
    public void HasOverlappingSessions_ReturnsFalseWhenSessionsDoNotOverlap()
    {
        var userId = ObjectId.GenerateNewId();
        var sessions = new List<WorkSession>
        {
            new()
            {
                UserId = userId,
                StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                UserId = userId,
                StartTime = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)
            }
        };

        Assert.False(Totals.HasOverlappingSessions(sessions));
    }
}