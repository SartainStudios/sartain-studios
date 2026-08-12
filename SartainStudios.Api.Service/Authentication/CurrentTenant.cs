using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using SartainStudios.Schema.Authentication;

namespace SartainStudios.Api.Service.Authentication;

public sealed class CurrentTenant(IHttpContextAccessor httpContextAccessor)
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public ObjectId UserId =>
        ObjectId.TryParse(User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : ObjectId.Empty;

    public ObjectId OrganizationId =>
        ObjectId.TryParse(User?.FindFirstValue(nameof(JwtClaimName.OrganizationId)), out var id)
            ? id
            : ObjectId.Empty;

    public ObjectId SessionId =>
        ObjectId.TryParse(User?.FindFirstValue(nameof(JwtClaimName.SessionId)), out var id)
            ? id
            : ObjectId.Empty;

    public string? Role => User?.FindFirstValue(ClaimTypes.Role);
    public string? Email => User?.FindFirstValue(JwtRegisteredClaimNames.Email);
    public string? DisplayName => User?.FindFirstValue(JwtRegisteredClaimNames.Name);

    public bool TryGetIdentity(out ObjectId userId, out ObjectId organizationId)
    {
        userId = UserId;
        organizationId = OrganizationId;
        return userId != ObjectId.Empty && organizationId != ObjectId.Empty;
    }

    public bool HasRole(params string[] roles)
    {
        return !string.IsNullOrWhiteSpace(Role) && roles.Contains(Role);
    }
}