namespace SartainStudios.Schema.WorkSession;

public sealed record UpdateRequest(DateTime StartTime, DateTime? EndTime);