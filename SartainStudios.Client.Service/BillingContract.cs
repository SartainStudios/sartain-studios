using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.Billing;

namespace SartainStudios.Client.Service;

public sealed class BillingContract(HttpClient httpClient)
{
    public async Task<IReadOnlyList<Summary>> ListAsync(string? projectId = null)
    {
        var url = "api/billing-contracts";
        if (!string.IsNullOrWhiteSpace(projectId)) url += $"?projectId={Uri.EscapeDataString(projectId)}";
        var response = await httpClient.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Summary>>() ?? [];
    }

    public async Task<Summary> GetAsync(string id)
    {
        var response = await httpClient.GetAsync($"api/billing-contracts/{id}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty billing contract response.");
    }

    public async Task<Summary> CreateAsync(CreateRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/billing-contracts", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty billing contract response.");
    }

    public async Task<Summary> UpdateAsync(string id, UpdateRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/billing-contracts/{id}", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty billing contract response.");
    }

    public async Task DeleteAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"api/billing-contracts/{id}");
        await EnsureSuccessAsync(response);
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        return Response.EnsureSuccessAsync(response);
    }
}