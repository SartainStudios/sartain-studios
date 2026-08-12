namespace SartainStudios.Api.Schema.AppSettings;

public sealed class Mongo
{
    public const string SectionName = nameof(Mongo);
    public required string ConnectionUri { get; init; }
    public required string DatabaseName { get; init; }
}