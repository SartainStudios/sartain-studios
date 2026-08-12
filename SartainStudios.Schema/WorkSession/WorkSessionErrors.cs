using SartainStudios.Schema.Api;

namespace SartainStudios.Schema.WorkSession;

public static class WorkSessionErrors
{
    public const string ContractIdField = "contractId";
    public const string StartTimeField = "startTime";
    public const string EndTimeField = "endTime";
    public const string TakeField = "take";
    public const string DayStartField = "dayStart";
    public const string WeekStartField = "weekStart";
    public const int MinimumTake = 1;
    public const int MaximumTake = 100;

    public static readonly Error InvalidId = Error.Validation(
        "WorkSession.InvalidId",
        "The supplied time session id is not a valid identifier.");

    public static readonly Error InvalidContractId = Error.Validation(
        "WorkSession.InvalidContractId",
        "The supplied billing contract id is not a valid identifier.");

    public static readonly Error ContractNotFound = Error.NotFound(
        "WorkSession.ContractNotFound",
        "Billing contract not found.");

    public static readonly Error ContractInactive = Error.Conflict(
        "WorkSession.ContractInactive",
        "Cannot start a timer with an inactive billing contract.");

    public static readonly Error ProjectNotFound = Error.NotFound(
        "WorkSession.ProjectNotFound",
        "The contract's project could not be found.");

    public static readonly Error ProjectArchived = Error.Conflict(
        "WorkSession.ProjectArchived",
        "Cannot start a timer for an archived project.");

    public static readonly Error TimerAlreadyRunning = Error.Conflict(
        "WorkSession.TimerAlreadyRunning",
        "A timer is already running.");

    public static readonly Error NoRunningTimer = Error.NotFound(
        "WorkSession.NoRunningTimer",
        "No running timer was found.");

    public static readonly Error StartTimeMustBeUtc = ValidationError.FromErrors(
        (StartTimeField, "Start time must be in UTC."));

    public static readonly Error EndTimeMustBeUtc = ValidationError.FromErrors(
        (EndTimeField, "End time must be in UTC."));

    public static readonly Error StartAndEndTimeMustBeUtc = ValidationError.FromErrors(
        (StartTimeField, "Start and end times must be in UTC."),
        (EndTimeField, "Start and end times must be in UTC."));

    public static readonly Error StartTimeInFuture = ValidationError.FromErrors(
        (StartTimeField, "Start time cannot be in the future."));

    public static readonly Error EndTimeInFuture = ValidationError.FromErrors(
        (EndTimeField, "End time cannot be in the future."));

    public static readonly Error EndBeforeStart = ValidationError.FromErrors(
        (EndTimeField, "End time cannot be earlier than the session start time."));

    public static readonly Error OverlapConflict = Error.Conflict(
        "WorkSession.Overlap",
        "The requested time range overlaps an existing time session.");

    public static readonly Error NotEditableBilled = Error.Conflict(
        "WorkSession.NotEditableBilled",
        "Cannot edit a billed time session.");

    public static readonly Error NotDiscardableBilled = Error.Conflict(
        "WorkSession.NotDiscardableBilled",
        "Cannot discard a billed time session.");

    public static readonly Error UpdateConflict = Error.Conflict(
        "WorkSession.UpdateConflict",
        "The time session could not be updated because it changed state.");

    public static readonly Error DiscardConflict = Error.Conflict(
        "WorkSession.DiscardConflict",
        "The time session could not be discarded because it changed state.");

    public static readonly Error StartConflict = Error.Conflict(
        "WorkSession.StartConflict",
        "A timer is already running.");

    public static readonly Error StopConflict = Error.Conflict(
        "WorkSession.StopConflict",
        "The running timer could not be stopped because it changed state.");

    public static readonly Error TakeOutOfRange = ValidationError.FromErrors(
        (TakeField, $"Take must be between {MinimumTake} and {MaximumTake}."));

    public static readonly Error BoundariesMustBeUtc = ValidationError.FromErrors(
        (DayStartField, "All boundaries must be provided in UTC."),
        (WeekStartField, "All boundaries must be provided in UTC."));

    public static readonly Error DayEndBeforeStart = ValidationError.FromErrors(
        (DayStartField, "Day end must be after day start."));

    public static readonly Error WeekEndBeforeStart = ValidationError.FromErrors(
        (WeekStartField, "Week end must be after week start."));

    public static readonly Error DayOutsideWeek = ValidationError.FromErrors(
        (DayStartField, "Day range must fall within the week range."));

    public static Error NotFound(string id)
    {
        return Error.NotFound(
            "WorkSession.NotFound",
            $"Time session with ID {id} was not found.");
    }
}