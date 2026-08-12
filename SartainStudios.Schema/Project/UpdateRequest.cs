namespace SartainStudios.Schema.Project;

public sealed record UpdateRequest(
    string ClientId,
    string Name,
    string Description,
    string Status);