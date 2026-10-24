using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Gig;

public sealed class GigReportService(
    Database database,
    CurrentTenant currentTenant,
    GigSettingsService settingsService,
    TimeProvider timeProvider)
{
    public async Task<Result<Report>> GetAsync(
        int weeks,
        string? userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        if (weeks is < GigErrors.MinimumWeeks or > GigErrors.MaximumWeeks)
            return GigErrors.WeeksOutOfRange;
        var timeZone = GigCalculator.ResolveTimeZone(userTimeZoneId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var firstWeekStart = GigCalculator.WeekStart(GigCalculator.LocalDate(now, timeZone))
            .AddDays(-7 * (Math.Max(weeks, 2) - 1));
        var windowStart = GigCalculator.LocalMidnightToUtc(firstWeekStart, timeZone);
        var sessions = await database.GigSessions
            .Find(session => session.UserId == userId && !session.IsOpen && session.StartTime >= windowStart)
            .ToListAsync(cancellationToken);
        var rate = await settingsService.GetRateContextAsync(userId, cancellationToken);
        return GigReportBuilder.Build(sessions, rate, timeZone, now, weeks);
    }
}