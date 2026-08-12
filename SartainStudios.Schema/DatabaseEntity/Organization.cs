namespace SartainStudios.Schema.DatabaseEntity;

public sealed class Organization : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public Address Address { get; set; } = new();
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}