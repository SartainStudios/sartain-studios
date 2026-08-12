using SartainStudios.Schema.Configuration;

namespace SartainStudios.Client.Configuration;

public sealed class ApiSettings
{
    public const string SectionName = nameof(ConfigurationSections.Api);
    public required string BaseUrl { get; init; }
}