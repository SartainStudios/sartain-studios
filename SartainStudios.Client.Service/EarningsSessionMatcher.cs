using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Service;

public static class EarningsSessionMatcher
{
    public static string? Suggest(DateTime date, TimeSpan startTime, TimeSpan? endTime,
        IEnumerable<GigSessionDto> sessions, TimeZoneInfo zone)
    {
        if (endTime == startTime)
            return null;
        var start = DateTime.SpecifyKind(date.Date + startTime, DateTimeKind.Unspecified);
        var end = DateTime.SpecifyKind(date.Date + (endTime ?? startTime), DateTimeKind.Unspecified);
        if (end < start) end = end.AddDays(1);
        end = end.AddMinutes(1);
        if (zone.IsInvalidTime(start) || zone.IsAmbiguousTime(start) ||
            zone.IsInvalidTime(end) || zone.IsAmbiguousTime(end))
            return null;
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(start, zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(end, zone);
        var matches = sessions.Where(session =>
                !session.IsOpen && session.EndTime is not null &&
                session.StartTime.ToUniversalTime() <= startUtc && session.EndTime.Value.ToUniversalTime() >= endUtc)
            .Take(2).ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }
}