using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using NSubstitute;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Schema.Authentication;

namespace SartainStudios.Api.Service.Test.Authentication;

public sealed class CurrentTenantTests
{
    [Fact]
    public void Properties_ReadValuesFromClaimsPrincipal()
    {
        var userId = ObjectId.GenerateNewId();
        var organizationId = ObjectId.GenerateNewId();
        var sessionId = ObjectId.GenerateNewId();
        var tenant = CreateTenant(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(nameof(JwtClaimName.OrganizationId), organizationId.ToString()),
            new Claim(nameof(JwtClaimName.SessionId), sessionId.ToString()),
            new Claim(ClaimTypes.Role, "Owner"),
            new Claim(JwtRegisteredClaimNames.Email, "tenant@example.com"),
            new Claim(JwtRegisteredClaimNames.Name, "Tenant User")
        });

        Assert.True(tenant.IsAuthenticated);
        Assert.Equal(userId, tenant.UserId);
        Assert.Equal(organizationId, tenant.OrganizationId);
        Assert.Equal(sessionId, tenant.SessionId);
        Assert.Equal("Owner", tenant.Role);
        Assert.Equal("tenant@example.com", tenant.Email);
        Assert.Equal("Tenant User", tenant.DisplayName);
    }

    [Fact]
    public void TryGetIdentity_ReturnsFalseWhenClaimsAreMissing()
    {
        var tenant = CreateTenant([]);

        var resolved = tenant.TryGetIdentity(out var userId, out var organizationId);

        Assert.False(resolved);
        Assert.Equal(ObjectId.Empty, userId);
        Assert.Equal(ObjectId.Empty, organizationId);
    }

    [Fact]
    public void HasRole_ReturnsTrueOnlyWhenRoleMatches()
    {
        var tenant = CreateTenant([new Claim(ClaimTypes.Role, "Administrator")]);

        Assert.True(tenant.HasRole("Owner", "Administrator"));
        Assert.False(tenant.HasRole("Owner", "Member"));
    }

    private static CurrentTenant CreateTenant(IEnumerable<Claim> claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new CurrentTenant(accessor);
    }
}