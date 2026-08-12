using System.Net.Http.Json;
using MudBlazor;
using SartainStudios.Client.Schema;
using SartainStudios.Schema.Authentication;

namespace SartainStudios.Client.Service.Authentication;

public sealed class TokenRefresher(
    TokenStore tokenStore,
    IHttpClientFactory httpClientFactory,
    ISnackbar snackbar)
{
    public const string RefreshClientName = "AuthenticationRefresh";
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private static readonly TimeSpan ExpiryLeeway = TimeSpan.FromSeconds(30);
    public event Action? SessionChanged;

    public static bool IsAccessTokenExpired(StoredSession session)
    {
        return session.AccessTokenExpiresAt <= DateTime.UtcNow.Add(ExpiryLeeway);
    }

    public async Task<StoredSession?> GetValidSessionAsync(CancellationToken cancellationToken = default)
    {
        var session = await tokenStore.LoadAsync();
        if (session is null) return null;
        if (IsAccessTokenExpired(session))
            session = await RefreshAsync(session, cancellationToken);
        return session;
    }

    public async Task<StoredSession?> RefreshAsync(StoredSession session, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.RefreshToken) || session.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            await ClearAsync();
            return null;
        }

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            var current = await tokenStore.LoadAsync();
            if (current is not null && !IsAccessTokenExpired(current) &&
                !string.IsNullOrWhiteSpace(current.AccessToken))
                return current;
            var refreshToken = current?.RefreshToken ?? session.RefreshToken;
            var client = httpClientFactory.CreateClient(RefreshClientName);
            var response = await client.PostAsJsonAsync(
                "api/authentication/refresh",
                new RefreshRequest(refreshToken),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await ClearAsync();
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<Response>(cancellationToken);
            if (body is null || string.IsNullOrWhiteSpace(body.AccessToken))
            {
                await ClearAsync();
                return null;
            }

            var updated = new StoredSession
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
            };
            await tokenStore.SaveAsync(updated);
            SessionChanged?.Invoke();
            return updated;
        }
        catch
        {
            snackbar.Add("Failed to refresh access token.", Severity.Error);
            return null;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    public async Task ClearAsync()
    {
        await tokenStore.ClearAsync();
        SessionChanged?.Invoke();
    }
}