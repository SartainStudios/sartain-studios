namespace SartainStudios.Schema.DatabaseEntity;

public sealed class UserProfile : AuditableEntity
{
    public string DisplayName { get; set; } = "User";
    public string? ProfilePhotoUrl { get; set; }
    public bool IsAdministrator { get; init; }
}