namespace SartainStudios.Api.Schema.AppSettings;

public sealed class ClientSettings
{
    public const string SectionName = nameof(ClientSettings);
    public required string BaseUrl { get; init; }
}