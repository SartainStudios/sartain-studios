using SartainStudios.Schema.Api;

namespace SartainStudios.Schema.Billing;

public static class BillingContractErrors
{
    public const string ProjectIdField = "projectId";
    public const string HourlyRateField = "hourlyRate";
    public const string ExpectedMinutesField = "expectedMinutes";
    public const string BillingCycleField = "billingCycle";
    public const string ServiceProvidedField = "serviceProvided";
    public const string InvoicePrefixField = "invoicePrefix";
    public const string ProjectIdRequired = "A project is required.";
    public const string HourlyRateRequired = "Hourly rate must be greater than zero.";
    public const string ExpectedMinutesRequired = "Expected minutes must be greater than zero.";
    public const string ServiceProvidedRequired = "Service provided is required.";
    public const string InvoicePrefixRequired = "Invoice prefix is required.";

    public static readonly Error InvalidId = Error.Validation(
        "BillingContract.InvalidId",
        "The supplied billing contract id is not a valid identifier.");

    public static readonly Error InvalidProjectId = Error.Validation(
        "BillingContract.InvalidProjectId",
        "The supplied project id is not a valid identifier.");

    public static readonly Error ProjectNotFound = Error.NotFound(
        "BillingContract.ProjectNotFound",
        "The project for this billing contract could not be found.");

    public static readonly Error ActiveContractExists = Error.Conflict(
        "BillingContract.ActiveContractExists",
        "A project can only have one active billing contract.");

    public static readonly Error HasWorkSessions = Error.Conflict(
        "BillingContract.HasWorkSessions",
        "Cannot delete a billing contract that has time sessions.");

    public static Error NotFound(string id)
    {
        return Error.NotFound(
            "BillingContract.NotFound",
            $"Billing contract with ID {id} was not found.");
    }

    public static string BillingCycleInvalid(string options)
    {
        return $"Billing cycle must be one of: {options}.";
    }
}