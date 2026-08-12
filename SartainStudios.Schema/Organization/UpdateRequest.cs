namespace SartainStudios.Schema.Organization;

public record UpdateRequest(
    string Name,
    Address? Address,
    string? Email,
    string? PhoneNumber);