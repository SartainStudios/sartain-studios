using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using NSubstitute;
using SartainStudios.Api.Service.Authentication;

namespace SartainStudios.Api.Service.Test.Infrastructure;

internal static class TestTenant
{
    public const string SubjectClaim = "sub";
    public const string OrganizationClaim = "OrganizationId";

    public static CurrentTenant Create(ObjectId? userId = null, ObjectId? organizationId = null)
    {
        var claims = new List<Claim>();

        if (userId is { } resolvedUserId) claims.Add(new Claim(SubjectClaim, resolvedUserId.ToString()));

        if (organizationId is { } resolvedOrganizationId)
            claims.Add(new Claim(OrganizationClaim, resolvedOrganizationId.ToString()));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        return new CurrentTenant(accessor);
    }

    public static CurrentTenant Anonymous()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        return new CurrentTenant(accessor);
    }
}