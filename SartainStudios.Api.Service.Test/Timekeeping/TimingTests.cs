using MongoDB.Bson;
using SartainStudios.Api.Service.Timekeeping;
using SartainStudios.Schema.WorkSession;

namespace SartainStudios.Api.Service.Test.Timekeeping;

public sealed class TimingTests
{
    [Fact]
    public void TruncateToMinute_RemovesSubMinuteComponents()
    {
        var input = new DateTime(2026, 1, 1, 10, 30, 45, 123, DateTimeKind.Utc);
        var result = Timing.TruncateToMinute(input);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 30, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void ParseContractId_ReturnsIdWhenValid()
    {
        var id = ObjectId.GenerateNewId();
        var result = Timing.ParseContractId(id.ToString());
        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value);
    }

    [Fact]
    public void ParseContractId_ReturnsErrorWhenInvalid()
    {
        var result = Timing.ParseContractId("not-an-id");
        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.InvalidContractId, result.Error);
    }

    [Fact]
    public void ParseOptionalContractId_ReturnsNullWhenEmpty()
    {
        var result = Timing.ParseOptionalContractId(null);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ParseOptionalContractId_ReturnsIdWhenValid()
    {
        var id = ObjectId.GenerateNewId();
        var result = Timing.ParseOptionalContractId(id.ToString());
        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value);
    }

    [Fact]
    public void ParseOptionalContractId_ReturnsErrorWhenInvalid()
    {
        var result = Timing.ParseOptionalContractId("bad-value");
        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.InvalidContractId, result.Error);
    }

    [Fact]
    public void NormalizeStartTime_TruncatesToMinuteWhenNull()
    {
        var now = new DateTime(2026, 1, 1, 10, 30, 45, DateTimeKind.Utc);
        var result = Timing.NormalizeStartTime(null, now);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 30, 0, DateTimeKind.Utc), result.Value);
    }

    [Fact]
    public void NormalizeStartTime_ReturnsErrorWhenNotUtc()
    {
        var local = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
        var result = Timing.NormalizeStartTime(local, DateTime.UtcNow);
        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.StartTimeMustBeUtc, result.Error);
    }

    [Fact]
    public void NormalizeStartTime_TruncatesToMinuteWhenUtc()
    {
        var startTime = new DateTime(2026, 1, 1, 9, 15, 30, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var result = Timing.NormalizeStartTime(startTime, now);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 1, 1, 9, 15, 0, DateTimeKind.Utc), result.Value);
    }

    [Fact]
    public void NormalizeEndTime_TruncatesToMinuteWhenNull()
    {
        var startTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 1, 10, 30, 45, DateTimeKind.Utc);
        var result = Timing.NormalizeEndTime(null, startTime, now);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 30, 0, DateTimeKind.Utc), result.Value);
    }

    [Fact]
    public void NormalizeEndTime_ReturnsErrorWhenNotUtc()
    {
        var startTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var local = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Local);
        var result = Timing.NormalizeEndTime(local, startTime, DateTime.UtcNow);
        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.EndTimeMustBeUtc, result.Error);
    }

    [Fact]
    public void NormalizeEndTime_ReturnsErrorWhenBeforeStart()
    {
        var startTime = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var result = Timing.NormalizeEndTime(endTime, startTime, now);
        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.EndBeforeStart, result.Error);
    }

    [Fact]
    public void NormalizeRange_ReturnsErrorWhenStartNotUtc()
    {
        var local = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Local);
        var result = Timing.NormalizeRange(local, null, DateTime.UtcNow);
        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.StartTimeMustBeUtc, result.Error);
    }

    [Fact]
    public void NormalizeRange_ReturnsErrorWhenBothNotUtc()
    {
        var local = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Local);
        var localEnd = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Local);
        var result = Timing.NormalizeRange(local, localEnd, DateTime.UtcNow);
        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.StartAndEndTimeMustBeUtc, result.Error);
    }

    [Fact]
    public void NormalizeRange_ReturnsOpenRangeWhenNoEndTime()
    {
        var startTime = new DateTime(2026, 1, 1, 8, 0, 30, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var result = Timing.NormalizeRange(startTime, null, now);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc), result.Value.StartTime);
        Assert.Null(result.Value.EndTime);
    }

    [Fact]
    public void NormalizeRange_ReturnsClosedRangeWhenEndTimeProvided()
    {
        var startTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var result = Timing.NormalizeRange(startTime, endTime, now);
        Assert.True(result.IsSuccess);
        Assert.Equal(startTime, result.Value.StartTime);
        Assert.Equal(endTime, result.Value.EndTime);
    }

    [Fact]
    public void ElapsedMinutes_ReturnsCorrectMinutes()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 9, 30, 0, DateTimeKind.Utc);
        Assert.Equal(90, Timing.ElapsedMinutes(start, end));
    }

    [Fact]
    public void ElapsedMinutes_ReturnsZeroWhenEndBeforeStart()
    {
        var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, Timing.ElapsedMinutes(start, end));
    }

    [Fact]
    public void OverlapMinutes_ReturnsCorrectOverlap()
    {
        var sessionStart = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var sessionEnd = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var rangeStart = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var rangeEnd = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        Assert.Equal(60, Timing.OverlapMinutes(sessionStart, sessionEnd, rangeStart, rangeEnd));
    }

    [Fact]
    public void OverlapMinutes_ReturnsZeroWhenNoOverlap()
    {
        var sessionStart = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var sessionEnd = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var rangeStart = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var rangeEnd = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, Timing.OverlapMinutes(sessionStart, sessionEnd, rangeStart, rangeEnd));
    }
}