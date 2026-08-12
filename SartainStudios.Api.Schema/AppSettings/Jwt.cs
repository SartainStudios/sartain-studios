using SartainStudios.Schema.Configuration;

namespace SartainStudios.Api.Schema.AppSettings;

public sealed class Jwt
{
    public const string SectionName = $"{nameof(ConfigurationSections.Authentication)}:{nameof(Jwt)}";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
    public required int AccessTokenMinutes { get; init; }
    public required int RefreshTokenDays { get; init; }
}