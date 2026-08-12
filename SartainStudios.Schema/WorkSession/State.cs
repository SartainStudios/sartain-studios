namespace SartainStudios.Schema.WorkSession;

public sealed record State(
    bool HasRunningSession,
    Session? CurrentSession,
    DateTime ServerTime);