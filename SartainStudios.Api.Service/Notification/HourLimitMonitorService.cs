using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Schema.Notification;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;
using SartainStudios.Schema.WorkSession;
using EmailSettings = SartainStudios.Api.Schema.AppSettings.Email;
using MonitorSettings = SartainStudios.Api.Schema.AppSettings.HourLimitMonitor;
using WorkSession = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Notification;

public sealed class HourLimitMonitorService(
    Database database,
    IEmail email,
    EmailSettings emailSettings,
    MonitorSettings monitorSettings,
    ILogger<HourLimitMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(30, monitorSettings.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Hour limit monitor scan failed; it will retry on the next interval.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        var memberships = await database.Memberships
            .Find(m => m.Status == nameof(RoleStatus.Active) && m.WeeklyHourLimitMinutes != null)
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0) return;
        var now = DateTime.UtcNow;
        var weekStart = GetWeekStart(now);
        var weekEnd = weekStart.AddDays(7);
        foreach (var membership in memberships)
        {
            if (membership.UserId == ObjectId.Empty || string.IsNullOrWhiteSpace(membership.Email)) continue;
            var limitMinutes = membership.WeeklyHourLimitMinutes!.Value;
            if (limitMinutes <= 0) continue;
            var warningMinutes = Math.Max(0, membership.HourLimitWarningMinutes);
            var weekWorkedMinutes = await GetWeekWorkedMinutesAsync(
                membership.OrganizationId, membership.UserId, weekStart, weekEnd, now, cancellationToken);
            var warningThreshold = Math.Max(0, limitMinutes - warningMinutes);
            if (weekWorkedMinutes >= limitMinutes)
                await TryNotifyAsync(membership, weekStart, HourLimitNotificationType.Reached, weekWorkedMinutes,
                    limitMinutes, cancellationToken);
            else if (weekWorkedMinutes >= warningThreshold)
                await TryNotifyAsync(membership, weekStart, HourLimitNotificationType.Approaching, weekWorkedMinutes,
                    limitMinutes, cancellationToken);
        }
    }

    private async Task<int> GetWeekWorkedMinutesAsync(ObjectId organizationId, ObjectId userId, DateTime weekStart,
        DateTime weekEnd, DateTime now, CancellationToken cancellationToken)
    {
        var filter = Builders<WorkSession>.Filter.Eq(x => x.OrganizationId, organizationId)
                     & Builders<WorkSession>.Filter.Eq(x => x.UserId, userId)
                     & Builders<WorkSession>.Filter.Lt(x => x.StartTime, weekEnd)
                     & Builders<WorkSession>.Filter.Or(
                         Builders<WorkSession>.Filter.Eq(x => x.EndTime, null),
                         Builders<WorkSession>.Filter.Gt(x => x.EndTime, weekStart));
        var sessions = await database.TimeSessions
            .Find(filter)
            .Project(x => new { x.StartTime, x.EndTime })
            .ToListAsync(cancellationToken);
        return sessions.Sum(session =>
        {
            var sessionEnd = session.EndTime ?? now;
            var overlapStart = session.StartTime > weekStart ? session.StartTime : weekStart;
            var overlapEnd = sessionEnd < weekEnd ? sessionEnd : weekEnd;
            return overlapEnd > overlapStart ? (int)Math.Floor((overlapEnd - overlapStart).TotalMinutes) : 0;
        });
    }

    private async Task TryNotifyAsync(
        SartainStudios.Schema.DatabaseEntity.Membership membership,
        DateTime weekStart,
        HourLimitNotificationType notificationType,
        int weekWorkedMinutes,
        int limitMinutes,
        CancellationToken cancellationToken)
    {
        var record = new HourLimitNotification
        {
            OrganizationId = membership.OrganizationId,
            UserId = membership.UserId,
            WeekStart = weekStart,
            NotificationType = notificationType.ToString()
        };
        var alreadyNotified = await database.HourLimitNotifications
            .Find(x => x.OrganizationId == membership.OrganizationId && x.UserId == membership.UserId &&
                       x.WeekStart == weekStart && x.NotificationType == record.NotificationType)
            .AnyAsync(cancellationToken);
        if (alreadyNotified) return;
        try
        {
            await database.HourLimitNotifications.InsertOneAsync(record, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return;
        }

        SendEmail(membership.Email, notificationType, weekWorkedMinutes, limitMinutes);
    }

    private void SendEmail(string recipientEmail, HourLimitNotificationType notificationType, int weekWorkedMinutes,
        int limitMinutes)
    {
        var workedHours = weekWorkedMinutes / 60.0;
        var limitHours = limitMinutes / 60.0;
        var subject = notificationType == HourLimitNotificationType.Reached
            ? "You've reached your weekly hour limit"
            : "You're approaching your weekly hour limit";
        var body = notificationType == HourLimitNotificationType.Reached
            ? $"You've logged {workedHours:0.##} hours this week, reaching your {limitHours:0.##}-hour weekly limit."
            : $"You've logged {workedHours:0.##} hours this week, approaching your {limitHours:0.##}-hour weekly limit.";
        email.SendEmail(new EmailRequest(
            [recipientEmail],
            [],
            emailSettings.Sender,
            subject,
            body,
            null!));
    }

    private static DateTime GetWeekStart(DateTime utcNow)
    {
        var daysSinceMonday = ((int)utcNow.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return utcNow.Date.AddDays(-daysSinceMonday);
    }
}