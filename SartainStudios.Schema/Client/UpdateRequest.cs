namespace SartainStudios.Schema.Client;

public sealed record UpdateRequest(
    string CompanyName,
    string ContactPerson,
    Address Address,
    string Email,
    string PhoneNumber);