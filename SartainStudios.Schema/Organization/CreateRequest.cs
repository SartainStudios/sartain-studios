namespace SartainStudios.Schema.Organization;

public record CreateRequest(
    string Name,
    Address? Address,
    string? Email,
    string? PhoneNumber);