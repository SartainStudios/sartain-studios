using SartainStudios.Schema.Api;

namespace SartainStudios.Schema.Project;

public static class ProjectErrors
{
    public const string ClientIdField = "clientId";
    public const string NameField = "name";
    public const string DescriptionField = "description";
    public const string StatusField = "status";
    public const string ClientIdRequired = "Project client is required.";
    public const string NameRequired = "Project name is required.";
    public const string DescriptionRequired = "Project description is required.";

    public static readonly Error InvalidId = Error.Validation(
        "Project.InvalidId",
        "The supplied project id is not a valid identifier.");

    public static readonly Error InvalidClientId = Error.Validation(
        "Project.InvalidClientId",
        "The supplied client id is not a valid identifier.");

    public static readonly Error ClientNotFound = Error.NotFound(
        "Project.ClientNotFound",
        "Client not found.");

    public static readonly Error HasBillingContracts = Error.Conflict(
        "Project.HasBillingContracts",
        "Cannot delete a project that still has billing contracts.");

    public static readonly Error HasWorkSessions = Error.Conflict(
        "Project.HasWorkSessions",
        "Cannot delete a project that still has time sessions.");

    public static Error NotFound(string id)
    {
        return Error.NotFound(
            "Project.NotFound",
            $"Project with ID {id} was not found.");
    }

    public static string StatusInvalid(string options)
    {
        return $"Project status must be one of: {options}.";
    }
}