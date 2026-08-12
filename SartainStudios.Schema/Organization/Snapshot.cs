namespace SartainStudios.Schema.Organization;

public sealed class Snapshot
{
    public string Name { get; init; } = string.Empty;
    public Address Address { get; init; } = new();
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
}