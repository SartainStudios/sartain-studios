namespace SartainStudios.Schema.WorkSession;

public sealed record History(
    string SessionId,
    string OrganizationId,
    string UserId,
    string ContractId,
    string ProjectId,
    string ProjectName,
    string ServiceProvided,
    string? InvoiceId,
    DateTime StartTime,
    DateTime? EndTime,
    int ElapsedMinutes,
    bool IsRunning,
    bool CanDiscard,
    bool CanEdit);