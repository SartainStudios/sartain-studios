using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace SartainStudios.Client.Service.Authentication;

public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly ISnackbar snackbar;
    private readonly TokenRefresher tokenRefresher;

    public JwtAuthenticationStateProvider(TokenRefresher tokenRefresher, ISnackbar snackbar)
    {
        this.tokenRefresher = tokenRefresher;
        this.snackbar = snackbar;
        tokenRefresher.SessionChanged += NotifyChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var session = await tokenRefresher.GetValidSessionAsync();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
            return Anonymous;
        var claims = ParseClaims(session.AccessToken);
        var identity = new ClaimsIdentity(claims, "jwt", JwtRegisteredClaimNames.Name, ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private IEnumerable<Claim> ParseClaims(string jwt)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);
            return token.Claims;
        }
        catch
        {
            snackbar.Add("Failed to parse JWT.", Severity.Error);
            return [];
        }
    }
}