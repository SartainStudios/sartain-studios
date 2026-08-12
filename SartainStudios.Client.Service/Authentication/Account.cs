using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.User;
using ApiResponse = SartainStudios.Client.Schema.Api.Response;

namespace SartainStudios.Client.Service.Authentication;

public sealed class Account(HttpClient httpClient, Authentication authentication)
{
    private const string BaseRoute = "api/account";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<AccountResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/me", cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<AccountResponse> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/profile", request, cancellationToken);
        var account = await ReadAsync(response, cancellationToken);
        await authentication.RefreshLocalUserAsync(account.User);
        return account;
    }

    public async Task<AccountResponse> UpdateNotificationPreferencesAsync(
        NotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{BaseRoute}/notification-preferences", request, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<AccountResponse> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{BaseRoute}/password", request, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<AccountResponse> UnlinkProviderAsync(
        IdentityProvider provider,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/identities/{provider}", cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    public async Task DeleteAccountAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(BaseRoute, cancellationToken);
        await ApiResponse.EnsureSuccessAsync(response);
    }

    private static async Task<AccountResponse> ReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await ApiResponse.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AccountResponse>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Empty account response.");
    }
}