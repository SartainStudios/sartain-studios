using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.WorkSession;

namespace SartainStudios.Client.Service;

public sealed class WorkSession(HttpClient httpClient)
{
    public async Task<State> GetCurrentAsync()
    {
        var response = await httpClient.GetAsync("api/work-sessions/current");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<State>()
               ?? throw new InvalidOperationException("Empty time session response.");
    }

    public async Task<State> StartAsync(string contractId, DateTime? startTime = null)
    {
        var response =
            await httpClient.PostAsJsonAsync("api/work-sessions/start", new StartRequest(contractId, startTime));
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<State>()
               ?? throw new InvalidOperationException("Empty time session response.");
    }

    public async Task<State> StopAsync(DateTime? endTime = null)
    {
        var response = await httpClient.PostAsJsonAsync("api/work-sessions/stop", new StopRequest(endTime));
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<State>()
               ?? throw new InvalidOperationException("Empty time session response.");
    }

    public async Task<History> GetAsync(string id)
    {
        var response = await httpClient.GetAsync($"api/work-sessions/{id}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<History>()
               ?? throw new InvalidOperationException("Empty time session response.");
    }

    public async Task<IReadOnlyList<History>> ListAsync(string? contractId = null, int take = 25)
    {
        var url = $"api/work-sessions?take={take}";
        if (!string.IsNullOrWhiteSpace(contractId)) url += $"&contractId={Uri.EscapeDataString(contractId)}";
        var response = await httpClient.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<History>>()
               ?? new List<History>();
    }

    public async Task<IReadOnlyList<Progress>> GetProgressAsync(string? contractId = null)
    {
        var url = "api/work-sessions/progress";
        if (!string.IsNullOrWhiteSpace(contractId)) url += $"?contractId={Uri.EscapeDataString(contractId)}";
        var response = await httpClient.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Progress>>()
               ?? new List<Progress>();
    }

    public async Task<TimeBudget> GetTimeBudgetAsync(
        DateTime dayStart, DateTime dayEnd, DateTime weekStart, DateTime weekEnd)
    {
        var url = "api/work-sessions/time-budget" +
                  $"?dayStart={Uri.EscapeDataString(dayStart.ToUniversalTime().ToString("o"))}" +
                  $"&dayEnd={Uri.EscapeDataString(dayEnd.ToUniversalTime().ToString("o"))}" +
                  $"&weekStart={Uri.EscapeDataString(weekStart.ToUniversalTime().ToString("o"))}" +
                  $"&weekEnd={Uri.EscapeDataString(weekEnd.ToUniversalTime().ToString("o"))}";
        var response = await httpClient.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TimeBudget>()
               ?? throw new InvalidOperationException("Empty time budget response.");
    }

    public async Task DiscardAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"api/work-sessions/{id}");
        await EnsureSuccessAsync(response);
    }

    public async Task<History> UpdateAsync(string id, UpdateRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/work-sessions/{id}", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<History>()
               ?? throw new InvalidOperationException("Empty time session response.");
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        return Response.EnsureSuccessAsync(response);
    }
}