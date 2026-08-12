using MongoDB.Bson;
using SartainStudios.Schema.Api;
using WorkSessionErrors = SartainStudios.Schema.WorkSession.WorkSessionErrors;

namespace SartainStudios.Api.Service.Timekeeping;

public static class Timing
{
    public static DateTime TruncateToMinute(DateTime value)
    {
        return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMinute));
    }

    public static Result<ObjectId> ParseContractId(string? contractId)
    {
        return ObjectId.TryParse(contractId, out var parsedContractId)
            ? parsedContractId
            : WorkSessionErrors.InvalidContractId;
    }

    public static Result<ObjectId?> ParseOptionalContractId(string? contractId)
    {
        if (string.IsNullOrWhiteSpace(contractId)) return Result.Success<ObjectId?>(null);
        return ObjectId.TryParse(contractId, out var value)
            ? Result.Success<ObjectId?>(value)
            : WorkSessionErrors.InvalidContractId;
    }

    public static Result<DateTime> NormalizeStartTime(DateTime? startTime, DateTime now)
    {
        if (!startTime.HasValue) return TruncateToMinute(now);
        if (startTime.Value.Kind != DateTimeKind.Utc)
            return WorkSessionErrors.StartTimeMustBeUtc;
        return TruncateToMinute(startTime.Value);
    }

    public static Result<DateTime> NormalizeEndTime(DateTime? endTime, DateTime startTime, DateTime now)
    {
        if (!endTime.HasValue) return TruncateToMinute(now);
        if (endTime.Value.Kind != DateTimeKind.Utc)
            return WorkSessionErrors.EndTimeMustBeUtc;
        var normalizedEndTime = TruncateToMinute(endTime.Value);
        if (normalizedEndTime < startTime)
            return WorkSessionErrors.EndBeforeStart;
        if (normalizedEndTime > now)
            return WorkSessionErrors.EndTimeInFuture;
        return normalizedEndTime;
    }

    public static Result<(DateTime StartTime, DateTime? EndTime)> NormalizeRange(
        DateTime startTime, DateTime? endTime, DateTime now)
    {
        if (startTime.Kind != DateTimeKind.Utc)
            return endTime.HasValue
                ? WorkSessionErrors.StartAndEndTimeMustBeUtc
                : WorkSessionErrors.StartTimeMustBeUtc;
        if (endTime.HasValue && endTime.Value.Kind != DateTimeKind.Utc)
            return WorkSessionErrors.EndTimeMustBeUtc;
        var normalizedStartTime = TruncateToMinute(startTime);
        if (normalizedStartTime > now)
            return WorkSessionErrors.StartTimeInFuture;
        if (!endTime.HasValue)
            return (normalizedStartTime, null);
        var normalizedEndTime = TruncateToMinute(endTime.Value);
        if (normalizedEndTime < normalizedStartTime)
            return WorkSessionErrors.EndBeforeStart;
        if (normalizedEndTime > now)
            return WorkSessionErrors.EndTimeInFuture;
        return (normalizedStartTime, normalizedEndTime);
    }

    public static int ElapsedMinutes(DateTime startTime, DateTime endTime)
    {
        return Math.Max(0, (int)Math.Floor((endTime - startTime).TotalMinutes));
    }

    public static int OverlapMinutes(DateTime sessionStart, DateTime sessionEnd, DateTime rangeStart, DateTime rangeEnd)
    {
        var overlapStart = sessionStart > rangeStart ? sessionStart : rangeStart;
        var overlapEnd = sessionEnd < rangeEnd ? sessionEnd : rangeEnd;
        return overlapEnd > overlapStart ? (int)Math.Floor((overlapEnd - overlapStart).TotalMinutes) : 0;
    }
}