namespace SartainStudios.Schema.Invoice;

public sealed record EditRequest(
    IReadOnlyList<string> SessionIds,
    DateTime DueDate);