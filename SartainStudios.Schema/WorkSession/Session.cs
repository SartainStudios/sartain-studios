namespace SartainStudios.Schema.WorkSession;

public sealed record Session(
    string SessionId,
    string OrganizationId,
    string UserId,
    string ContractId,
    string ProjectId,
    string ProjectName,
    string ServiceProvided,
    DateTime StartTime,
    DateTime? EndTime,
    int ElapsedMinutes);