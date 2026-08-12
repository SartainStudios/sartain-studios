namespace SartainStudios.Schema.Client;

public sealed class Snapshot
{
    public string CompanyName { get; init; } = string.Empty;
    public string ContactPerson { get; init; } = string.Empty;
    public Address Address { get; init; } = new();
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
}