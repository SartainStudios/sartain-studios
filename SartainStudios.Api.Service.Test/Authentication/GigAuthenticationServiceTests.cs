using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using NSubstitute;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Api.Schema.Authentication;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Notification;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;
using Email = SartainStudios.Api.Schema.AppSettings.Email;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;

namespace SartainStudios.Api.Service.Test.Authentication;

public sealed class GigAuthenticationServiceTests
{
    private static readonly Jwt JwtSettings = new()
    {
        Issuer = "issuer-test",
        Audience = "audience-test",
        SigningKey = "this-is-a-long-signing-key-for-tests",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 7
    };

    [Fact]
    public async Task GoogleSignInAsync_ReturnsValidationErrorWhenIdTokenMissing()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest(""));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AuthenticationErrors.IdTokenRequired, validation.Errors[AuthenticationErrors.IdTokenField]);
    }

    [Fact]
    public async Task GoogleSignInAsync_ReturnsInvalidTokenWhenValidatorFails()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("id-token", Arg.Any<CancellationToken>()).Returns((GoogleIdentity?)null);

        var result = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("id-token"));

        Assert.Equal(AuthenticationErrors.GoogleTokenInvalid, result.Error);
    }

    [Fact]
    public async Task GoogleSignInAsync_ReturnsUnverifiedWhenEmailIsNotVerified()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("id-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleIdentity("sub-1", "user@example.com", false, "User", null));

        var result = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("id-token"));

        Assert.Equal(AuthenticationErrors.GoogleEmailUnverified, result.Error);
    }

    [Fact]
    public async Task GoogleSignInAsync_NewUserCreatesProfileAndIdentityButNeverAnOrganizationOrMembership()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "gig@example.com", true, "Gig Driver", "https://photo.test/a.png"));

        var result = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("google-token"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsNewUser);
        Assert.Single(fixture.Harness.UserProfiles.Documents);
        Assert.Single(fixture.Harness.AuthenticationIdentities.Documents);
        Assert.Empty(fixture.Harness.Organizations.Documents);
        Assert.Empty(fixture.Harness.Memberships.Documents);
        Assert.Empty(fixture.Harness.Organizations.Inserted);
        Assert.Empty(fixture.Harness.Memberships.Inserted);
    }

    [Fact]
    public async Task GoogleSignInAsync_NewUserSessionHasNoOrganization()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "gig@example.com", true, "Gig Driver", null));

        var result = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("google-token"));

        var storedSession = Assert.Single(fixture.Harness.AuthenticationSessions.Documents);
        Assert.Equal(ObjectId.Empty, storedSession.OrganizationId);
        Assert.Equal(fixture.Harness.UserProfiles.Documents.Single().Id, storedSession.UserId);
        Assert.NotEmpty(result.Value.RefreshToken);
    }

    [Fact]
    public async Task GoogleSignInAsync_NewUserAccessTokenCarriesNoOrganizationOrRoleClaim()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "gig@example.com", true, "Gig Driver", null));

        var result = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("google-token"));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.AccessToken);
        var user = fixture.Harness.UserProfiles.Documents.Single();
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("Gig Driver", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Name).Value);
        Assert.Equal("gig@example.com", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(fixture.Harness.AuthenticationSessions.Documents.Single().Id.ToString(),
            jwt.Claims.Single(c => c.Type == nameof(JwtClaimName.SessionId)).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == nameof(JwtClaimName.OrganizationId));
        Assert.DoesNotContain(jwt.Claims, c => c.Type is "role" or ClaimTypes.Role);
    }

    [Fact]
    public async Task GoogleSignInAsync_ExistingUserWithoutMembershipStaysOrganizationLess()
    {
        var fixture = CreateFixture();
        var user = SeedGoogleUser(fixture, "sub-1", "gig@example.com");
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "gig@example.com", true, "Gig Driver", null));

        var result = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("google-token"));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsNewUser);
        Assert.Empty(fixture.Harness.Organizations.Documents);
        Assert.Empty(fixture.Harness.Memberships.Documents);
        var storedSession = Assert.Single(fixture.Harness.AuthenticationSessions.Documents);
        Assert.Equal(ObjectId.Empty, storedSession.OrganizationId);
        Assert.Equal(user.Id, storedSession.UserId);
    }

    [Fact]
    public async Task GoogleSignInAsync_ExistingMemberKeepsTheirOrganizationSessionAndCreatesNothing()
    {
        var fixture = CreateFixture();
        var user = SeedGoogleUser(fixture, "sub-1", "member@example.com");
        var organization = new OrganizationEntity { Name = "Org" };
        fixture.Harness.Organizations.Seed(organization);
        fixture.Harness.Memberships.Seed(new MembershipEntity
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Active),
            Email = "member@example.com"
        });
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "member@example.com", true, "Member", null));

        var result = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("google-token"));

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.Harness.Organizations.Documents);
        Assert.Single(fixture.Harness.Memberships.Documents);
        Assert.Empty(fixture.Harness.Organizations.Inserted);
        Assert.Empty(fixture.Harness.Memberships.Inserted);
        Assert.Equal(organization.Id, fixture.Harness.AuthenticationSessions.Documents.Single().OrganizationId);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.AccessToken);
        Assert.Equal(organization.Id.ToString(),
            jwt.Claims.Single(c => c.Type == nameof(JwtClaimName.OrganizationId)).Value);
    }

    [Fact]
    public async Task GoogleSignInAsync_ReturnsProviderConflictWhenEmailBelongsToAnotherProvider()
    {
        var fixture = CreateFixture();
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Email,
            ProviderSubject = "gig@example.com",
            Email = "gig@example.com"
        });
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "gig@example.com", true, "Gig Driver", null));

        var result = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("google-token"));

        Assert.Equal("Authentication.ProviderConflict", result.Error.Code);
        Assert.Empty(fixture.Harness.UserProfiles.Documents);
    }

    [Fact]
    public async Task RefreshAsync_ReissuesOrganizationLessSessionAndRevokesTheOldOne()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "gig@example.com", true, "Gig Driver", null));
        var signedIn = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("google-token"));

        var refreshed = await fixture.Service.RefreshAsync(new RefreshRequest(signedIn.Value.RefreshToken));

        Assert.True(refreshed.IsSuccess);
        Assert.NotEqual(signedIn.Value.RefreshToken, refreshed.Value.RefreshToken);
        Assert.Equal(2, fixture.Harness.AuthenticationSessions.Documents.Count);
        Assert.Single(fixture.Harness.AuthenticationSessions.Documents, s => s.RevokedAt is not null);
        Assert.All(fixture.Harness.AuthenticationSessions.Documents,
            s => Assert.Equal(ObjectId.Empty, s.OrganizationId));
        Assert.Empty(fixture.Harness.Organizations.Documents);
        Assert.Empty(fixture.Harness.Memberships.Documents);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(refreshed.Value.AccessToken);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == nameof(JwtClaimName.OrganizationId));
    }

    [Fact]
    public async Task RefreshAsync_RejectsUnknownRefreshToken()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.RefreshAsync(new RefreshRequest("unknown"));

        Assert.Equal(AuthenticationErrors.RefreshTokenInvalid, result.Error);
    }

    [Fact]
    public async Task InvoicingRefreshAsync_FailsSafelyForOrganizationLessGigSession()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "gig@example.com", true, "Gig Driver", null));
        var signedIn = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("google-token"));
        var invoicing = CreateInvoicingService(fixture.Harness);

        var result = await invoicing.RefreshAsync(new RefreshRequest(signedIn.Value.RefreshToken));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.SessionExpired, result.Error);
        Assert.Empty(fixture.Harness.Organizations.Documents);
        Assert.Empty(fixture.Harness.Memberships.Documents);
    }

    [Fact]
    public async Task GigAccessToken_CannotResolveAnInvoicingTenantIdentity()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "gig@example.com", true, "Gig Driver", null));
        var result = await fixture.Service.GoogleSignInAsync(new GigGoogleSignInRequest("google-token"));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.AccessToken);
        var tenant = TenantFromClaims(jwt.Claims);

        Assert.False(tenant.TryGetIdentity(out _, out _));
        Assert.NotEqual(ObjectId.Empty, tenant.UserId);
        Assert.Equal(ObjectId.Empty, tenant.OrganizationId);
    }

    private static UserProfile SeedGoogleUser(Fixture fixture, string subject, string email)
    {
        var user = new UserProfile { DisplayName = "Gig Driver" };
        fixture.Harness.UserProfiles.Seed(user);
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = user.Id,
            Provider = IdentityProvider.Google,
            ProviderSubject = subject,
            Email = email,
            EmailVerified = true
        });
        return user;
    }

    private static CurrentTenant TenantFromClaims(IEnumerable<Claim> claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(claims, "Test"))
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new CurrentTenant(accessor);
    }

    private static AuthenticationService CreateInvoicingService(MongoHarness harness)
    {
        var now = new DateTimeOffset(new DateTime(2026, 8, 11, 15, 0, 0, DateTimeKind.Utc));
        var timeProvider = new StaticTimeProvider(now);
        var token = new Token(JwtSettings);
        return new AuthenticationService(
            harness.Database,
            token,
            new Password(harness.Database),
            new Session(harness.Database, token, timeProvider),
            new Provisioning(harness.Database),
            TestTenant.Anonymous(),
            Substitute.For<IGoogleIdentityValidator>(),
            Substitute.For<IEmail>(),
            new Email
            {
                Host = "smtp.test", Port = 25, Username = "u", Password = "p", Sender = "no-reply@test.local"
            },
            new ClientSettings { BaseUrl = "https://client.test" },
            timeProvider);
    }

    private static Fixture CreateFixture()
    {
        var harness = new MongoHarness();
        var googleValidator = Substitute.For<IGoogleIdentityValidator>();
        var timeProvider =
            new StaticTimeProvider(new DateTimeOffset(new DateTime(2026, 8, 11, 15, 0, 0, DateTimeKind.Utc)));
        var session = new Session(harness.Database, new Token(JwtSettings), timeProvider);
        var service = new GigAuthenticationService(harness.Database, session, googleValidator, timeProvider);
        return new Fixture(service, harness, googleValidator);
    }

    private sealed record Fixture(
        GigAuthenticationService Service,
        MongoHarness Harness,
        IGoogleIdentityValidator GoogleValidator);
}