namespace SartainStudios.Schema.WorkSession;

public sealed record TimeBudget(
    int DayWorkedMinutes,
    int DayTargetMinutes,
    int DayRemainingMinutes,
    int WeekWorkedMinutes,
    int WeekTargetMinutes,
    int WeekRemainingMinutes);