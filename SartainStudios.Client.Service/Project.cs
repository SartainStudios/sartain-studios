using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.Project;

namespace SartainStudios.Client.Service;

public sealed class Project(HttpClient httpClient)
{
    public async Task<IReadOnlyList<Summary>> ListAsync()
    {
        var response = await httpClient.GetAsync("api/projects");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Summary>>() ?? [];
    }

    public async Task<Summary> GetAsync(string id)
    {
        var response = await httpClient.GetAsync($"api/projects/{id}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty project response.");
    }

    public async Task<Summary> CreateAsync(CreateRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/projects", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty project response.");
    }

    public async Task<Summary> UpdateAsync(string id, UpdateRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/projects/{id}", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty project response.");
    }

    public async Task DeleteAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"api/projects/{id}");
        await EnsureSuccessAsync(response);
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        return Response.EnsureSuccessAsync(response);
    }
}