namespace SartainStudios.Schema.Invoice;

public sealed record SelectableSession(
    string SessionId,
    string OrganizationId,
    string UserId,
    string ContractId,
    string ProjectId,
    string ProjectName,
    string ServiceProvided,
    DateTime StartTime,
    DateTime EndTime,
    int MinutesWorked);