using Microsoft.Extensions.Diagnostics.HealthChecks;
using SartainStudios.Schema.Health;

namespace SartainStudios.Api.Service.Health;

public sealed class HealthService(HealthCheckService healthCheckService)
{
    public HealthReportResponse CheckLiveness()
    {
        return HealthReportResponse.Live;
    }

    public Task<HealthReportResponse> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        return CheckAsync(check => check.Tags.Contains("ready"), cancellationToken);
    }

    public Task<HealthReportResponse> CheckAsync(CancellationToken cancellationToken)
    {
        return CheckAsync(_ => true, cancellationToken);
    }

    private async Task<HealthReportResponse> CheckAsync(
        Func<HealthCheckRegistration, bool> predicate,
        CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(predicate, cancellationToken);
        var checks = report.Entries
            .Select(entry => new HealthCheckEntryResponse(
                entry.Key,
                entry.Value.Status.ToString(),
                entry.Value.Description,
                entry.Value.Duration))
            .ToList();
        return new HealthReportResponse(report.Status.ToString(), report.TotalDuration, checks);
    }
}