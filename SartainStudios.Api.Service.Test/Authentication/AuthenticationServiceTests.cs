using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Api.Schema.Authentication;
using SartainStudios.Api.Schema.Notification;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Notification;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;
using AppEmailSettings = SartainStudios.Api.Schema.AppSettings.Email;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;

namespace SartainStudios.Api.Service.Test.Authentication;

public sealed class AuthenticationServiceTests
{
    private static readonly Jwt JwtSettings = new()
    {
        Issuer = "issuer-test",
        Audience = "audience-test",
        SigningKey = "this-is-a-long-signing-key-for-tests",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 7
    };

    private static readonly AppEmailSettings EmailSettings = new()
    {
        Host = "smtp.test",
        Port = 25,
        Username = "user",
        Password = "pass",
        Sender = "no-reply@test.local"
    };

    private static readonly ClientSettings ClientSettings = new()
    {
        BaseUrl = "https://client.test"
    };

    [Fact]
    public async Task GoogleSignInAsync_ReturnsValidationErrorWhenIdTokenMissing()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.GoogleSignInAsync(new GoogleSignInRequest("", null, null, null, null));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AuthenticationErrors.IdTokenRequired, validation.Errors[AuthenticationErrors.IdTokenField]);
    }

    [Fact]
    public async Task GoogleSignInAsync_ReturnsInvalidTokenWhenValidatorFails()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("id-token", Arg.Any<CancellationToken>()).Returns((GoogleIdentity?)null);

        var result =
            await fixture.Service.GoogleSignInAsync(new GoogleSignInRequest("id-token", null, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.GoogleTokenInvalid, result.Error);
    }

    [Fact]
    public async Task GoogleSignInAsync_ReturnsUnverifiedWhenGoogleEmailIsNotVerified()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("id-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleIdentity("sub-1", "user@example.com", false, "User", null));

        var result =
            await fixture.Service.GoogleSignInAsync(new GoogleSignInRequest("id-token", null, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.GoogleEmailUnverified, result.Error);
    }

    [Fact]
    public async Task GoogleSignInAsync_ReturnsMissingEmailWhenGoogleIdentityHasNoEmail()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("id-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleIdentity("sub-1", "", true, "User", null));

        var result =
            await fixture.Service.GoogleSignInAsync(new GoogleSignInRequest("id-token", null, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.GoogleEmailMissing, result.Error);
    }

    [Fact]
    public async Task GoogleSignInAsync_RegistersUserWhenIdentityDoesNotExist()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "google@example.com", true, "Google User", "https://photo.test/a.png"));

        var result =
            await fixture.Service.GoogleSignInAsync(new GoogleSignInRequest("google-token", "Org", null, null, null));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsNewUser);
        Assert.Single(fixture.Harness.UserProfiles.Documents);
        Assert.Single(fixture.Harness.AuthenticationIdentities.Documents);
        Assert.Single(fixture.Harness.Organizations.Documents);
        Assert.Single(fixture.Harness.Memberships.Documents);
        Assert.Single(fixture.Harness.AuthenticationSessions.Documents);
    }

    [Fact]
    public async Task GoogleSignInAsync_ReturnsProviderConflictWhenEmailExistsOnDifferentProvider()
    {
        var fixture = CreateFixture();
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "google@example.com", true, "Google User", null));
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Email,
            ProviderSubject = "google@example.com",
            Email = "google@example.com",
            EmailVerified = false
        });

        var result =
            await fixture.Service.GoogleSignInAsync(new GoogleSignInRequest("google-token", null, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.ProviderConflict", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task GoogleSignInAsync_SignsInExistingGoogleIdentity()
    {
        var fixture = CreateFixture();
        var user = new UserProfile { DisplayName = "Google User", ProfilePhotoUrl = "https://old.photo" };
        var organization = new OrganizationEntity { Name = "Org" };
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Role = "Owner",
            Status = nameof(RoleStatus.Active),
            Email = "google@example.com"
        };
        fixture.Harness.UserProfiles.Seed(user);
        fixture.Harness.Organizations.Seed(organization);
        fixture.Harness.Memberships.Seed(membership);
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = user.Id,
            Provider = IdentityProvider.Google,
            ProviderSubject = "sub-1",
            Email = "google@example.com",
            EmailVerified = true
        });
        fixture.GoogleValidator.ValidateAsync("google-token", Arg.Any<CancellationToken>()).Returns(
            new GoogleIdentity("sub-1", "google@example.com", true, "Google User", "https://new.photo"));

        var result =
            await fixture.Service.GoogleSignInAsync(new GoogleSignInRequest("google-token", null, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsNewUser);
        Assert.Equal("https://new.photo", fixture.Harness.UserProfiles.Documents.Single().ProfilePhotoUrl);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsValidationErrorForInvalidCredentials()
    {
        var fixture = CreateFixture();

        var result =
            await fixture.Service.RegisterAsync(new EmailRegisterRequest("bad-email", "123", null, null, null, null,
                null));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AuthenticationErrors.EmailInvalid, validation.Errors[AuthenticationErrors.EmailField]);
        Assert.Contains(AuthenticationErrors.PasswordTooShort(Password.MinimumLength),
            validation.Errors[AuthenticationErrors.PasswordField]);
    }

    [Fact]
    public async Task RegisterAsync_CreatesEmailIdentityAndWorkspace()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.RegisterAsync(new EmailRegisterRequest(
            "  NewUser@Example.com ",
            "LongPassword123!",
            "  New User  ",
            "My Org",
            null,
            null,
            null));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsNewUser);
        Assert.Equal("newuser@example.com", result.Value.User.Email);
        Assert.Single(fixture.Harness.UserProfiles.Documents);
        Assert.Single(fixture.Harness.AuthenticationIdentities.Documents);
        Assert.Single(fixture.Harness.EmailPasswordCredentials.Documents);
        Assert.Single(fixture.Harness.Memberships.Documents);
        Assert.Single(fixture.Harness.Organizations.Documents);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsEmailAlreadyRegisteredWhenEmailIdentityExists()
    {
        var fixture = CreateFixture();
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Email,
            ProviderSubject = "already@example.com",
            Email = "already@example.com",
            EmailVerified = true
        });

        var result = await fixture.Service.RegisterAsync(new EmailRegisterRequest(
            "already@example.com",
            "Password123!",
            null,
            null,
            null,
            null,
            null));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.EmailAlreadyRegistered, result.Error);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsProviderConflictWhenDifferentProviderUsesEmail()
    {
        var fixture = CreateFixture();
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Google,
            ProviderSubject = "subject",
            Email = "conflict@example.com",
            EmailVerified = true
        });

        var result = await fixture.Service.RegisterAsync(new EmailRegisterRequest(
            "conflict@example.com",
            "Password123!",
            null,
            null,
            null,
            null,
            null));

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.ProviderConflict", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task SignInAsync_ReturnsInvalidCredentialsWhenAccountMissing()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.SignInAsync(new EmailSignInRequest("user@example.com", "Password123!"));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task SignInAsync_ReturnsProviderConflictWhenGoogleIdentityUsesEmail()
    {
        var fixture = CreateFixture();
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Google,
            ProviderSubject = "subject",
            Email = "user@example.com",
            EmailVerified = true
        });

        var result = await fixture.Service.SignInAsync(new EmailSignInRequest("user@example.com", "Password123!"));

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.ProviderConflict", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task SignInAsync_ReturnsInvalidCredentialsWhenPasswordDoesNotMatch()
    {
        var fixture = CreateFixture();
        var user = new UserProfile { DisplayName = "Signed In User" };
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = user.Id,
            Provider = IdentityProvider.Email,
            ProviderSubject = "user@example.com",
            Email = "user@example.com",
            EmailVerified = true
        });
        fixture.Harness.EmailPasswordCredentials.Seed(new EmailPasswordCredential
        {
            UserId = user.Id,
            PasswordHash = Password.Hash("DifferentPassword123!")
        });

        var result = await fixture.Service.SignInAsync(new EmailSignInRequest("user@example.com", "Password123!"));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task SignInAsync_ReturnsSuccessForValidCredentials()
    {
        var fixture = CreateFixture();
        var user = new UserProfile { DisplayName = "Signed In User" };
        var organization = new OrganizationEntity { Name = "Acme" };
        var identity = new AuthenticationIdentity
        {
            UserId = user.Id,
            Provider = IdentityProvider.Email,
            ProviderSubject = "user@example.com",
            Email = "user@example.com",
            EmailVerified = false
        };
        var credential = new EmailPasswordCredential
        {
            UserId = user.Id,
            PasswordHash = Password.Hash("Password123!")
        };
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Role = "Owner",
            Status = nameof(RoleStatus.Active),
            Email = "user@example.com"
        };
        fixture.Harness.UserProfiles.Seed(user);
        fixture.Harness.Organizations.Seed(organization);
        fixture.Harness.AuthenticationIdentities.Seed(identity);
        fixture.Harness.EmailPasswordCredentials.Seed(credential);
        fixture.Harness.Memberships.Seed(membership);

        var result = await fixture.Service.SignInAsync(new EmailSignInRequest("user@example.com", "Password123!"));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsNewUser);
        Assert.Equal(user.Id.ToString(), result.Value.User.Id);
        Assert.Single(fixture.Harness.AuthenticationSessions.Inserted);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ReturnsValidationErrorForInvalidEmail()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest("not-an-email"));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AuthenticationErrors.EmailInvalid, validation.Errors[AuthenticationErrors.EmailField]);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ReturnsSuccessWhenIdentityDoesNotExist()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest("missing@example.com"));

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Harness.PasswordResetTokens.Documents);
        fixture.EmailSender.DidNotReceive().SendEmail(Arg.Any<EmailRequest>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_CreatesResetTokenAndSendsEmail()
    {
        var fixture = CreateFixture();
        var user = new UserProfile { DisplayName = "Forgot User" };
        fixture.Harness.UserProfiles.Seed(user);
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = user.Id,
            Provider = IdentityProvider.Email,
            ProviderSubject = "user@example.com",
            Email = "user@example.com",
            EmailVerified = true
        });

        var result = await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.Harness.PasswordResetTokens.Documents);
        fixture.EmailSender.Received(1).SendEmail(Arg.Any<EmailRequest>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_ReturnsFailureWhenEmailSendThrows()
    {
        var fixture = CreateFixture();
        var user = new UserProfile { DisplayName = "Forgot User" };
        fixture.Harness.UserProfiles.Seed(user);
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = user.Id,
            Provider = IdentityProvider.Email,
            ProviderSubject = "user@example.com",
            Email = "user@example.com",
            EmailVerified = true
        });
        fixture.EmailSender.When(x => x.SendEmail(Arg.Any<EmailRequest>()))
            .Do(_ => throw new SmtpException("smtp failure"));

        var result = await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.ResetEmailNotSent, result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_ReturnsValidationErrorForMissingTokenAndWeakPassword()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.ResetPasswordAsync(new ResetPasswordRequest("", "123"));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AuthenticationErrors.ResetTokenRequired, validation.Errors[AuthenticationErrors.TokenField]);
        Assert.Contains(AuthenticationErrors.PasswordTooShort(Password.MinimumLength),
            validation.Errors[AuthenticationErrors.NewPasswordField]);
    }

    [Fact]
    public async Task ResetPasswordAsync_ReturnsInvalidWhenTokenNotFound()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.ResetPasswordAsync(new ResetPasswordRequest("missing", "NewPassword123!"));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.ResetLinkInvalid, result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_ReturnsInvalidWhenTokenUserCannotBeFound()
    {
        var fixture = CreateFixture();
        var rawToken = "raw-reset-token";
        fixture.Harness.PasswordResetTokens.Seed(new PasswordResetToken
        {
            UserId = ObjectId.GenerateNewId(),
            TokenHash = fixture.Token.HashPasswordResetToken(rawToken),
            ExpiresAt = fixture.Now.UtcDateTime.AddMinutes(30)
        });

        var result = await fixture.Service.ResetPasswordAsync(new ResetPasswordRequest(rawToken, "NewPassword123!"));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.ResetLinkInvalid, result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdatesPasswordAndMarksTokenUsed()
    {
        var fixture = CreateFixture();
        fixture.Harness.PasswordResetTokens.Collection
            .UpdateManyAsync(
                Arg.Any<FilterDefinition<PasswordResetToken>>(),
                Arg.Any<UpdateDefinition<PasswordResetToken>>(),
                Arg.Any<UpdateOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(1, 1, 1)));
        var user = new UserProfile { DisplayName = "Reset User" };
        var rawToken = "raw-reset-token";
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = fixture.Token.HashPasswordResetToken(rawToken),
            ExpiresAt = fixture.Now.UtcDateTime.AddMinutes(30)
        };
        fixture.Harness.UserProfiles.Seed(user);
        fixture.Harness.PasswordResetTokens.Seed(resetToken);

        var result = await fixture.Service.ResetPasswordAsync(new ResetPasswordRequest(rawToken, "NewPassword123!"));

        Assert.True(result.IsSuccess);
        var storedToken = Assert.Single(fixture.Harness.PasswordResetTokens.Documents);
        Assert.NotNull(storedToken.UsedAt);
        var credential = Assert.Single(fixture.Harness.EmailPasswordCredentials.Documents);
        Assert.True(Password.Verify("NewPassword123!", credential.PasswordHash));
    }

    [Fact]
    public async Task RefreshAsync_ReturnsValidationErrorWhenRefreshTokenMissing()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.RefreshAsync(new RefreshRequest(""));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AuthenticationErrors.RefreshTokenRequired,
            validation.Errors[AuthenticationErrors.RefreshTokenField]);
    }

    [Fact]
    public async Task RefreshAsync_ReturnsInvalidWhenSessionMissing()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.RefreshAsync(new RefreshRequest("missing-token"));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.RefreshTokenInvalid, result.Error);
    }

    [Fact]
    public async Task RefreshAsync_ReturnsSessionExpiredWhenSessionReferencesMissingData()
    {
        var fixture = CreateFixture();
        var refreshToken = "active-refresh";
        fixture.Harness.AuthenticationSessions.Seed(new AuthenticationSession
        {
            UserId = ObjectId.GenerateNewId(),
            OrganizationId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Email,
            RefreshTokenHash = fixture.Token.HashRefreshToken(refreshToken),
            ExpiresAt = fixture.Now.UtcDateTime.AddDays(1)
        });

        var result = await fixture.Service.RefreshAsync(new RefreshRequest(refreshToken));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.SessionExpired, result.Error);
    }

    [Fact]
    public async Task RefreshAsync_RevokesCurrentSessionAndIssuesNewOne()
    {
        var fixture = CreateFixture();
        var user = new UserProfile { DisplayName = "Refresh User" };
        var organization = new OrganizationEntity { Name = "Refresh Org" };
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = nameof(RoleStatus.Active),
            Role = "Owner",
            Email = "refresh@example.com"
        };
        var refreshToken = "active-refresh";
        var activeSession = new AuthenticationSession
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Provider = IdentityProvider.Email,
            RefreshTokenHash = fixture.Token.HashRefreshToken(refreshToken),
            ExpiresAt = fixture.Now.UtcDateTime.AddDays(1)
        };
        fixture.Harness.UserProfiles.Seed(user);
        fixture.Harness.Organizations.Seed(organization);
        fixture.Harness.Memberships.Seed(membership);
        fixture.Harness.AuthenticationSessions.Seed(activeSession);

        var result = await fixture.Service.RefreshAsync(new RefreshRequest(refreshToken));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsNewUser);
        Assert.Equal(2, fixture.Harness.AuthenticationSessions.Documents.Count);
        var revoked = fixture.Harness.AuthenticationSessions.Documents.Single(x => x.Id == activeSession.Id);
        Assert.NotNull(revoked.RevokedAt);
    }

    [Fact]
    public async Task SignOutAsync_RevokesSessionByContextAndByRefreshToken()
    {
        var userId = ObjectId.GenerateNewId();
        var organizationId = ObjectId.GenerateNewId();
        var sessionId = ObjectId.GenerateNewId();
        var tenant = CreateTenant(userId, organizationId, sessionId, "Owner", "signout@example.com", "Sign Out User");
        var fixture = CreateFixture(tenant);
        var contextSession = new AuthenticationSession
        {
            Id = sessionId,
            UserId = userId,
            OrganizationId = organizationId,
            Provider = IdentityProvider.Email,
            RefreshTokenHash = fixture.Token.HashRefreshToken("context-token"),
            ExpiresAt = fixture.Now.UtcDateTime.AddDays(1)
        };
        var refreshSession = new AuthenticationSession
        {
            UserId = userId,
            OrganizationId = organizationId,
            Provider = IdentityProvider.Email,
            RefreshTokenHash = fixture.Token.HashRefreshToken("refresh-token"),
            ExpiresAt = fixture.Now.UtcDateTime.AddDays(1)
        };
        fixture.Harness.AuthenticationSessions.Seed(contextSession, refreshSession);

        var result = await fixture.Service.SignOutAsync(new SignOutRequest("refresh-token"));

        Assert.True(result.IsSuccess);
        Assert.All(fixture.Harness.AuthenticationSessions.Documents, session => Assert.NotNull(session.RevokedAt));
    }

    [Fact]
    public async Task SignOutAsync_IgnoresMissingRefreshToken()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.SignOutAsync(new SignOutRequest(null));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void GetCurrentUser_ReturnsNotResolvedWhenTenantMissing()
    {
        var fixture = CreateFixture(TestTenant.Anonymous());

        var result = fixture.Service.GetCurrentUser();

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public void GetCurrentUser_ReturnsResolvedUserDetails()
    {
        var userId = ObjectId.GenerateNewId();
        var organizationId = ObjectId.GenerateNewId();
        var sessionId = ObjectId.GenerateNewId();
        var tenant = CreateTenant(userId, organizationId, sessionId, "Administrator", "tenant@example.com",
            "Tenant User");
        var fixture = CreateFixture(tenant);

        var result = fixture.Service.GetCurrentUser();

        Assert.True(result.IsSuccess);
        Assert.Equal(userId.ToString(), result.Value.UserId);
        Assert.Equal(organizationId.ToString(), result.Value.OrganizationId);
        Assert.Equal("Tenant User", result.Value.DisplayName);
        Assert.Equal("tenant@example.com", result.Value.Email);
        Assert.Equal("Administrator", result.Value.Role);
    }

    private static Fixture CreateFixture(CurrentTenant? currentTenant = null)
    {
        var harness = new MongoHarness();
        var googleValidator = Substitute.For<IGoogleIdentityValidator>();
        var emailSender = Substitute.For<IEmail>();
        var now = new DateTimeOffset(new DateTime(2026, 8, 11, 15, 0, 0, DateTimeKind.Utc));
        var timeProvider = new StaticTimeProvider(now);
        var token = new Token(JwtSettings);
        var password = new Password(harness.Database);
        var session = new Session(harness.Database, token);
        var provisioning = new Provisioning(harness.Database);
        var tenant = currentTenant ?? TestTenant.Anonymous();
        var service = new AuthenticationService(
            harness.Database,
            token,
            password,
            session,
            provisioning,
            tenant,
            googleValidator,
            emailSender,
            EmailSettings,
            ClientSettings,
            timeProvider);

        return new Fixture(service, harness, googleValidator, emailSender, token, now);
    }

    private static CurrentTenant CreateTenant(
        ObjectId userId,
        ObjectId organizationId,
        ObjectId sessionId,
        string role,
        string email,
        string displayName)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(nameof(JwtClaimName.OrganizationId), organizationId.ToString()),
            new Claim(nameof(JwtClaimName.SessionId), sessionId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, displayName)
        };
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new CurrentTenant(accessor);
    }

    private sealed record Fixture(
        AuthenticationService Service,
        MongoHarness Harness,
        IGoogleIdentityValidator GoogleValidator,
        IEmail EmailSender,
        Token Token,
        DateTimeOffset Now);
}