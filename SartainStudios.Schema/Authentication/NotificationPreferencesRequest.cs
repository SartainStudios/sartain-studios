namespace SartainStudios.Schema.Authentication;

public record NotificationPreferencesRequest(
    int? WeeklyHourLimitMinutes,
    int HourLimitWarningMinutes);