namespace SartainStudios.Schema.Health;

public sealed record HealthCheckEntryResponse(
    string Name,
    string Status,
    string? Description,
    TimeSpan Duration);