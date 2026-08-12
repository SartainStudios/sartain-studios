using SartainStudios.Schema.Api;

namespace SartainStudios.Schema.Client;

public static class ClientErrors
{
    public const string CompanyNameField = "companyName";
    public const string ContactPersonField = "contactPerson";
    public const string EmailField = "email";
    public const string PhoneNumberField = "phoneNumber";
    public const string AddressField = "address";
    public const string AddressLine1Field = "address.line1";
    public const string AddressCityField = "address.city";
    public const string AddressStateOrProvinceField = "address.stateOrProvince";
    public const string AddressPostalCodeField = "address.postalCode";
    public const string AddressCountryField = "address.country";
    public const string CompanyNameRequired = "Client company name is required.";
    public const string ContactPersonRequired = "Client contact person is required.";
    public const string AddressRequired = "Client address is required.";
    public const string AddressLine1Required = "Client street address is required.";
    public const string AddressCityRequired = "Client city is required.";
    public const string AddressStateOrProvinceRequired = "Client state or province is required.";
    public const string AddressPostalCodeRequired = "Client postal code is required.";
    public const string AddressCountryRequired = "Client country is required.";
    public const string EmailRequired = "Client email is required.";
    public const string EmailInvalid = "Client email is invalid.";
    public const string PhoneNumberRequired = "Client phone number is required.";
    public const string PhoneNumberInvalid = "Client phone number is invalid.";

    public static readonly Error InvalidId = Error.Validation(
        "Client.InvalidId",
        "The supplied client id is not a valid identifier.");

    public static readonly Error HasProjects = Error.Conflict(
        "Client.HasProjects",
        "Cannot delete a client that still has projects.");

    public static Error NotFound(string id)
    {
        return Error.NotFound(
            "Client.NotFound",
            $"Client with ID {id} was not found.");
    }
}