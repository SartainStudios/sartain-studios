namespace SartainStudios.Schema.WorkSession;

public sealed record StartRequest(string ContractId, DateTime? StartTime);