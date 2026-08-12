namespace SartainStudios.Schema.Invoice;

public sealed record CreateRequest(
    string ContractId,
    IReadOnlyList<string> SessionIds,
    DateTime DueDate);