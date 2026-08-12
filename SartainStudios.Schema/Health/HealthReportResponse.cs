namespace SartainStudios.Schema.Health;

public sealed record HealthReportResponse(
    string Status,
    TimeSpan TotalDuration,
    IReadOnlyList<HealthCheckEntryResponse> Checks)
{
    public static readonly HealthReportResponse Live = new("Healthy", TimeSpan.Zero, []);
}