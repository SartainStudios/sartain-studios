namespace SartainStudios.Schema.Project;

public sealed record CreateRequest(
    string ClientId,
    string Name,
    string Description,
    string Status);