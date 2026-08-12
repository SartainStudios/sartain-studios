using System.Net.Http.Json;
using SartainStudios.Client.Schema;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Client.Service.Authentication;
using SartainStudios.Schema.Organization;

namespace SartainStudios.Client.Service;

public sealed class Organization(
    HttpClient httpClient,
    TokenStore tokenStore,
    JwtAuthenticationStateProvider stateProvider)
{
    public async Task<IReadOnlyList<OrganizationSummary>> ListMineAsync()
    {
        var response = await httpClient.GetAsync("api/organizations");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<OrganizationSummary>>()
               ?? new List<OrganizationSummary>();
    }

    public async Task<OrganizationSummary> GetAsync(string id)
    {
        var response = await httpClient.GetAsync($"api/organizations/{id}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<OrganizationSummary>()
               ?? throw new InvalidOperationException("Empty organization response.");
    }

    public async Task<OrganizationSummary> CreateAsync(CreateRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/organizations", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<OrganizationSummary>()
               ?? throw new InvalidOperationException("Empty organization response.");
    }

    public async Task<OrganizationSummary> UpdateAsync(string id, UpdateRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/organizations/{id}", request);
        await EnsureSuccessAsync(response);
        var body = await response.Content.ReadFromJsonAsync<OrganizationSummary>()
                   ?? throw new InvalidOperationException("Empty organization response.");
        var session = await tokenStore.LoadAsync();
        if (session is not null && session.OrganizationId == body.Id)
        {
            session.OrganizationName = body.Name;
            await tokenStore.SaveAsync(session);
            stateProvider.NotifyChanged();
        }

        return body;
    }

    public async Task<SwitchResponse> SwitchAsync(string id)
    {
        var response = await httpClient.PostAsync($"api/organizations/{id}/switch", null);
        await EnsureSuccessAsync(response);
        var body = await response.Content.ReadFromJsonAsync<SwitchResponse>()
                   ?? throw new InvalidOperationException("Empty switch response.");
        await tokenStore.SaveAsync(new StoredSession
        {
            AccessToken = body.AccessToken,
            AccessTokenExpiresAt = body.AccessTokenExpiresAt,
            RefreshToken = body.RefreshToken,
            RefreshTokenExpiresAt = body.RefreshTokenExpiresAt,
            UserId = body.User.Id,
            DisplayName = body.User.DisplayName,
            Email = body.User.Email,
            ProfilePhotoUrl = body.User.ProfilePhotoUrl,
            OrganizationId = body.Organization.Id,
            OrganizationName = body.Organization.Name,
            Role = body.Organization.Role
        });
        stateProvider.NotifyChanged();
        return body;
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        return Response.EnsureSuccessAsync(response);
    }
}