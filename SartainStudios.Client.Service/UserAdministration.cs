using System.Net.Http.Json;
using SartainStudios.Schema.User;
using ApiResponse = SartainStudios.Client.Schema.Api.Response;

namespace SartainStudios.Client.Service;

public sealed class UserAdministration(HttpClient httpClient)
{
    private const string BaseRoute = "api/administration/users";

    public async Task<bool> HasAccessAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("api/administration/access", cancellationToken);
        await ApiResponse.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken);
    }

    public async Task<IReadOnlyList<AdministratorUserResponse>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BaseRoute, cancellationToken);
        await ApiResponse.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<AdministratorUserResponse>>(cancellationToken)
               ?? throw new InvalidOperationException("Empty user list response.");
    }

    public async Task<AdministratorUserResponse> UpdateAsync(
        string id,
        AdministratorUpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{BaseRoute}/{Uri.EscapeDataString(id)}", request, cancellationToken);
        await ApiResponse.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AdministratorUserResponse>(cancellationToken)
               ?? throw new InvalidOperationException("Empty user response.");
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(
            UserTimeZone.Append($"{BaseRoute}/{Uri.EscapeDataString(id)}"), cancellationToken);
        await ApiResponse.EnsureSuccessAsync(response);
    }
}