namespace SartainStudios.Api.Schema.AppSettings;

public sealed class Email
{
    public const string SectionName = nameof(Email);
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string Sender { get; init; }
}