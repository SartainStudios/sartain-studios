namespace SartainStudios.Api.Schema.AppSettings;

public sealed class Cors
{
    public const string SectionName = nameof(Cors);
    public string[] AllowedOrigins { get; init; } = [];
}