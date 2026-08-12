namespace SartainStudios.Schema.Authentication;

public record AccountResponse(
    User User,
    bool IsAdministrator,
    IReadOnlyList<LinkedIdentityResponse> Identities,
    bool HasPassword,
    int? WeeklyHourLimitMinutes,
    int HourLimitWarningMinutes);