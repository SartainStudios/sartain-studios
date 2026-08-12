using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Client.Service.Caching;
using SartainStudios.Schema.Billing;

namespace SartainStudios.Client.Service;

public sealed class BillingContract(HttpClient httpClient, DataCache cache)
{
    public Task<IReadOnlyList<Summary>> ListAsync(string? projectId = null, bool forceRefresh = false)
    {
        return cache.GetOrFetchAsync(
            CacheKeys.BillingContractList(projectId),
            cancellationToken => FetchListAsync(projectId, cancellationToken),
            CachePolicy.Reference,
            forceRefresh);
    }

    public Task<Summary> GetAsync(string id, bool forceRefresh = false)
    {
        return cache.GetOrFetchAsync(
            CacheKeys.BillingContract(id),
            cancellationToken => FetchAsync(id, cancellationToken),
            CachePolicy.Reference,
            forceRefresh);
    }

    public async Task<Summary> CreateAsync(CreateRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/billing-contracts", request);
        await EnsureSuccessAsync(response);
        var created = await response.Content.ReadFromJsonAsync<Summary>()
                      ?? throw new InvalidOperationException("Empty billing contract response.");
        cache.InvalidatePrefix(CacheKeys.BillingContractPrefix);
        cache.Set(CacheKeys.BillingContract(created.Id), created);
        return created;
    }

    public async Task<Summary> UpdateAsync(string id, UpdateRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/billing-contracts/{id}", request);
        await EnsureSuccessAsync(response);
        var updated = await response.Content.ReadFromJsonAsync<Summary>()
                      ?? throw new InvalidOperationException("Empty billing contract response.");
        cache.InvalidatePrefix(CacheKeys.BillingContractPrefix);
        cache.Set(CacheKeys.BillingContract(id), updated);
        return updated;
    }

    public async Task DeleteAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"api/billing-contracts/{id}");
        await EnsureSuccessAsync(response);
        cache.InvalidatePrefix(CacheKeys.BillingContractPrefix);
    }

    private async Task<IReadOnlyList<Summary>> FetchListAsync(string? projectId, CancellationToken cancellationToken)
    {
        var url = "api/billing-contracts";
        if (!string.IsNullOrWhiteSpace(projectId)) url += $"?projectId={Uri.EscapeDataString(projectId)}";
        var response = await httpClient.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Summary>>(cancellationToken) ?? [];
    }

    private async Task<Summary> FetchAsync(string id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"api/billing-contracts/{id}", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>(cancellationToken)
               ?? throw new InvalidOperationException("Empty billing contract response.");
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        return Response.EnsureSuccessAsync(response);
    }
}