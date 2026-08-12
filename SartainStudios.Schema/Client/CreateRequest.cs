namespace SartainStudios.Schema.Client;

public sealed record CreateRequest(
    string CompanyName,
    string ContactPerson,
    Address Address,
    string Email,
    string PhoneNumber);