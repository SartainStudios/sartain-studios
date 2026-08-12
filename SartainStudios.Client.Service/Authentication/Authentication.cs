using System.Net.Http.Json;
using SartainStudios.Client.Schema;
using SartainStudios.Client.Service.Caching;
using SartainStudios.Schema.Authentication;
using ApiResponse = SartainStudios.Client.Schema.Api.Response;

namespace SartainStudios.Client.Service.Authentication;

public sealed class Authentication(
    HttpClient httpClient,
    TokenStore tokenStore,
    JwtAuthenticationStateProvider stateProvider,
    DataCache cache)
{
    private const string BaseRoute = "api/authentication";

    public Task<Response> RegisterAsync(
        EmailRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        return AuthenticateAsync($"{BaseRoute}/email/register", request, cancellationToken);
    }

    public Task<Response> SignInAsync(
        EmailSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        return AuthenticateAsync($"{BaseRoute}/email/sign-in", request, cancellationToken);
    }

    public Task<Response> GoogleSignInAsync(
        GoogleSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        return AuthenticateAsync($"{BaseRoute}/google/sign-in", request, cancellationToken);
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{BaseRoute}/forgot-password", request, cancellationToken);
        await ApiResponse.EnsureSuccessAsync(response);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{BaseRoute}/reset-password", request, cancellationToken);
        await ApiResponse.EnsureSuccessAsync(response);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/me", cancellationToken);
        await ApiResponse.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<CurrentUserResponse>(cancellationToken)
               ?? throw new InvalidOperationException("Empty current user response.");
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var session = await tokenStore.LoadAsync();
        if (session is not null)
            try
            {
                await httpClient.PostAsJsonAsync(
                    $"{BaseRoute}/sign-out", new SignOutRequest(session.RefreshToken), cancellationToken);
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

        await tokenStore.ClearAsync();
        cache.Clear();
        stateProvider.NotifyChanged();
    }

    public async Task RefreshLocalUserAsync(User user)
    {
        var session = await tokenStore.LoadAsync();
        if (session is null) return;
        session.DisplayName = user.DisplayName;
        session.Email = user.Email;
        session.ProfilePhotoUrl = user.ProfilePhotoUrl;
        await tokenStore.SaveAsync(session);
        stateProvider.NotifyChanged();
    }

    private async Task<Response> AuthenticateAsync<TRequest>(
        string route,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var httpResponse = await httpClient.PostAsJsonAsync(route, request, cancellationToken);
        await ApiResponse.EnsureSuccessAsync(httpResponse);
        var response = await httpResponse.Content.ReadFromJsonAsync<Response>(cancellationToken)
                       ?? throw new InvalidOperationException("Empty authentication response.");
        await PersistAsync(response);
        return response;
    }

    private async Task PersistAsync(Response response)
    {
        cache.Clear();
        await tokenStore.SaveAsync(new StoredSession
        {
            AccessToken = response.AccessToken,
            AccessTokenExpiresAt = response.AccessTokenExpiresAt,
            RefreshToken = response.RefreshToken,
            RefreshTokenExpiresAt = response.RefreshTokenExpiresAt,
            UserId = response.User.Id,
            DisplayName = response.User.DisplayName,
            Email = response.User.Email,
            ProfilePhotoUrl = response.User.ProfilePhotoUrl,
            OrganizationId = response.Organization.Id,
            OrganizationName = response.Organization.Name,
            Role = response.Organization.Role
        });
        stateProvider.NotifyChanged();
    }
}