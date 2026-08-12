using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Client.Service.Caching;
using SartainStudios.Schema.Project;

namespace SartainStudios.Client.Service;

public sealed class Project(HttpClient httpClient, DataCache cache)
{
    public Task<IReadOnlyList<Summary>> ListAsync(bool forceRefresh = false)
    {
        return cache.GetOrFetchAsync(CacheKeys.ProjectList, FetchListAsync, CachePolicy.Reference, forceRefresh);
    }

    public Task<Summary> GetAsync(string id, bool forceRefresh = false)
    {
        return cache.GetOrFetchAsync(
            CacheKeys.Project(id),
            cancellationToken => FetchAsync(id, cancellationToken),
            CachePolicy.Reference,
            forceRefresh);
    }

    public async Task<Summary> CreateAsync(CreateRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/projects", request);
        await EnsureSuccessAsync(response);
        var created = await response.Content.ReadFromJsonAsync<Summary>()
                      ?? throw new InvalidOperationException("Empty project response.");
        InvalidateAll();
        cache.Set(CacheKeys.Project(created.Id), created);
        return created;
    }

    public async Task<Summary> UpdateAsync(string id, UpdateRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/projects/{id}", request);
        await EnsureSuccessAsync(response);
        var updated = await response.Content.ReadFromJsonAsync<Summary>()
                      ?? throw new InvalidOperationException("Empty project response.");
        InvalidateAll();
        cache.Set(CacheKeys.Project(id), updated);
        return updated;
    }

    public async Task DeleteAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"api/projects/{id}");
        await EnsureSuccessAsync(response);
        InvalidateAll();
    }

    private async Task<IReadOnlyList<Summary>> FetchListAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/projects", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Summary>>(cancellationToken) ?? [];
    }

    private async Task<Summary> FetchAsync(string id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"api/projects/{id}", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>(cancellationToken)
               ?? throw new InvalidOperationException("Empty project response.");
    }

    private void InvalidateAll()
    {
        cache.InvalidatePrefix(CacheKeys.ProjectPrefix);
        cache.InvalidatePrefix(CacheKeys.BillingContractPrefix);
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        return Response.EnsureSuccessAsync(response);
    }
}