using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using JwtSettings = SartainStudios.Api.Schema.AppSettings.Jwt;

namespace SartainStudios.Api.ServiceCollection;

public static class Jwt
{
    public static void AddJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                          ?? throw new InvalidOperationException("Jwt authentication settings are required.");
        if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
            throw new InvalidOperationException(
                $"{JwtSettings.SectionName}:{nameof(JwtSettings.SigningKey)} is required.");
        if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) || string.IsNullOrWhiteSpace(jwtSettings.Audience))
            throw new InvalidOperationException(
                $"{JwtSettings.SectionName}:{nameof(JwtSettings.Issuer)} and {JwtSettings.SectionName}:{nameof(JwtSettings.Audience)} are required.");
        services.AddSingleton(jwtSettings);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
    }
}