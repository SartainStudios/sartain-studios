using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.Client;

namespace SartainStudios.Client.Service;

public sealed class Client(HttpClient httpClient)
{
    public async Task<IReadOnlyList<Summary>> ListAsync()
    {
        var response = await httpClient.GetAsync("api/clients");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Summary>>() ?? [];
    }

    public async Task<Summary> GetAsync(string id)
    {
        var response = await httpClient.GetAsync($"api/clients/{id}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty client response.");
    }

    public async Task<Summary> CreateAsync(CreateRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/clients", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty client response.");
    }

    public async Task<Summary> UpdateAsync(string id, UpdateRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/clients/{id}", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty client response.");
    }

    public async Task DeleteAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"api/clients/{id}");
        await EnsureSuccessAsync(response);
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        return Response.EnsureSuccessAsync(response);
    }
}