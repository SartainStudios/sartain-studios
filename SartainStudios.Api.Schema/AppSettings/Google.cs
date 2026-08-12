using SartainStudios.Schema.Configuration;

namespace SartainStudios.Api.Schema.AppSettings;

public sealed class Google
{
    public const string SectionName = $"{nameof(ConfigurationSections.Authentication)}:{nameof(Google)}";

    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
}