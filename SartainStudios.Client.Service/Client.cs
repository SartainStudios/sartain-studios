using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Client.Service.Caching;
using SartainStudios.Schema.Client;

namespace SartainStudios.Client.Service;

public sealed class Client(HttpClient httpClient, DataCache cache)
{
    public Task<IReadOnlyList<Summary>> ListAsync(bool forceRefresh = false)
    {
        return cache.GetOrFetchAsync(CacheKeys.ClientList, FetchListAsync, CachePolicy.Reference, forceRefresh);
    }

    public Task<Summary> GetAsync(string id, bool forceRefresh = false)
    {
        return cache.GetOrFetchAsync(
            CacheKeys.Client(id),
            cancellationToken => FetchAsync(id, cancellationToken),
            CachePolicy.Reference,
            forceRefresh);
    }

    public async Task<Summary> CreateAsync(CreateRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/clients", request);
        await EnsureSuccessAsync(response);
        var created = await response.Content.ReadFromJsonAsync<Summary>()
                      ?? throw new InvalidOperationException("Empty client response.");
        InvalidateAll();
        cache.Set(CacheKeys.Client(created.Id), created);
        return created;
    }

    public async Task<Summary> UpdateAsync(string id, UpdateRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/clients/{id}", request);
        await EnsureSuccessAsync(response);
        var updated = await response.Content.ReadFromJsonAsync<Summary>()
                      ?? throw new InvalidOperationException("Empty client response.");
        InvalidateAll();
        cache.Set(CacheKeys.Client(id), updated);
        return updated;
    }

    public async Task DeleteAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"api/clients/{id}");
        await EnsureSuccessAsync(response);
        InvalidateAll();
    }

    private async Task<IReadOnlyList<Summary>> FetchListAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/clients", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Summary>>(cancellationToken) ?? [];
    }

    private async Task<Summary> FetchAsync(string id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"api/clients/{id}", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>(cancellationToken)
               ?? throw new InvalidOperationException("Empty client response.");
    }

    private void InvalidateAll()
    {
        cache.InvalidatePrefix(CacheKeys.ClientPrefix);
        cache.InvalidatePrefix(CacheKeys.ProjectPrefix);
        cache.InvalidatePrefix(CacheKeys.BillingContractPrefix);
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        return Response.EnsureSuccessAsync(response);
    }
}