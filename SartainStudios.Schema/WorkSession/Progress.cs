namespace SartainStudios.Schema.WorkSession;

public sealed record Progress(
    string ContractId,
    int LoggedMinutes);