using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.Health;

namespace SartainStudios.Client.Service;

public sealed class Health(HttpClient httpClient)
{
    public async Task<HealthReportResponse> GetLivenessAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("api/health/live", cancellationToken);
        await Response.EnsureSuccessAsync(response);
        return await ReadReportAsync(response, cancellationToken);
    }

    public Task<HealthReportResponse> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        return GetReportAsync("api/health/ready", cancellationToken);
    }

    public Task<HealthReportResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        return GetReportAsync("api/health", cancellationToken);
    }

    private async Task<HealthReportResponse> GetReportAsync(string requestUri, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        var report = await response.Content.ReadFromJsonAsync<HealthReportResponse>(
            cancellationToken);
        if (report is not null) return report;
        await Response.EnsureSuccessAsync(response);
        throw new InvalidOperationException("Empty health response.");
    }

    private static async Task<HealthReportResponse> ReadReportAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<HealthReportResponse>(cancellationToken)
               ?? throw new InvalidOperationException("Empty health response.");
    }
}