using Google.Apis.Auth;
using SartainStudios.Api.Schema.Authentication;
using SartainStudios.Api.Service.Authentication;
using GoogleSettings = SartainStudios.Api.Schema.AppSettings.Google;

namespace SartainStudios.Api.Authentication;

public sealed class GoogleIdentityValidator(GoogleSettings googleSettings) : IGoogleIdentityValidator
{
    public async Task<GoogleIdentity?> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [googleSettings.ClientId] });
        }
        catch (InvalidJwtException)
        {
            return null;
        }

        return new GoogleIdentity(
            payload.Subject,
            payload.Email?.Trim().ToLowerInvariant() ?? string.Empty,
            payload.EmailVerified,
            BuildDisplayName(payload),
            payload.Picture);
    }

    private static string BuildDisplayName(GoogleJsonWebSignature.Payload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.Name))
            return payload.Name.Trim();
        var parts = new[] { payload.GivenName, payload.FamilyName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        if (parts.Length > 0)
            return string.Join(" ", parts).Trim();
        return payload.Email?.Trim() ?? "User";
    }
}