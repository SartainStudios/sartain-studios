using SartainStudios.Schema.Api;

namespace SartainStudios.Schema.Organization;

public static class OrganizationErrors
{
    public const string NameField = "name";
    public const string EmailField = "email";
    public const string PhoneNumberField = "phoneNumber";
    public const string NameRequired = "Organization name is required.";
    public const string EmailInvalid = "Organization email is invalid.";
    public const string PhoneNumberInvalid = "Organization phone number is invalid.";

    public static readonly Error InvalidId = Error.Validation(
        "Organization.InvalidId",
        "The supplied organization id is not a valid identifier.");

    public static readonly Error Forbidden = Error.Forbidden(
        "Organization.Forbidden",
        "You do not have an active membership in this organization.");

    public static readonly Error NotActiveOrganization = Error.Forbidden(
        "Organization.NotActive",
        "You may only edit the organization your current session is scoped to.");

    public static Error NotFound(string id)
    {
        return Error.NotFound(
            "Organization.NotFound",
            $"Organization with ID {id} was not found.");
    }
}