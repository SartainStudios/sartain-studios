using SartainStudios.Api.Schema.Authentication;

namespace SartainStudios.Api.Service.Authentication;

public interface IGoogleIdentityValidator
{
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}