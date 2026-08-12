using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.Membership;

namespace SartainStudios.Client.Service;

public sealed class Membership(HttpClient httpClient)
{
    public async Task<IReadOnlyList<Summary>> ListAsync()
    {
        var response = await httpClient.GetAsync("api/memberships");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Summary>>() ?? [];
    }

    public async Task<Summary> InviteAsync(InviteRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/memberships", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty membership response.");
    }

    public async Task<Summary> UpdateRoleAsync(string id, UpdateRequest request)
    {
        var response = await httpClient.PatchAsJsonAsync($"api/memberships/{id}/role", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty membership response.");
    }

    public async Task RemoveAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"api/memberships/{id}");
        await EnsureSuccessAsync(response);
    }

    public async Task<Summary> AcceptAsync(string id)
    {
        var response = await httpClient.PostAsync($"api/memberships/{id}/accept", null);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Summary>()
               ?? throw new InvalidOperationException("Empty membership response.");
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        return Response.EnsureSuccessAsync(response);
    }
}