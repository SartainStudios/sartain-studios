using SartainStudios.Schema.Api;

namespace SartainStudios.Schema.Invoice;

public static class InvoiceErrors
{
    public const int MinimumTake = 1;
    public const int MaximumTake = 100;
    public const string ClientIdField = "clientId";
    public const string ContractIdField = "contractId";
    public const string SessionIdsField = "sessionIds";
    public const string DueDateField = "dueDate";
    public const string StatusField = "status";
    public const string TakeField = "take";
    public const string ClientIdInvalid = "The supplied client id is not a valid identifier.";
    public const string ContractIdInvalid = "The supplied billing contract id is not a valid identifier.";
    public const string SessionIdsRequired = "At least one time session must be selected.";
    public const string SessionIdsInvalid = "One or more time session ids are invalid.";
    public const string SessionIdsNotUnique = "Time session ids must be unique.";
    public const string DueDateNotUtc = "Due date must be in UTC.";

    public static readonly Error InvalidId = Error.Validation(
        "Invoice.InvalidId",
        "The supplied invoice id is not a valid identifier.");

    public static readonly Error InvalidContractId = Error.Validation(
        "Invoice.InvalidContractId",
        ContractIdInvalid);

    public static readonly Error ContractNotFound = Error.NotFound(
        "Invoice.ContractNotFound",
        "Billing contract not found.");

    public static readonly Error ProjectNotFound = Error.NotFound(
        "Invoice.ProjectNotFound",
        "The contract's project could not be found.");

    public static readonly Error ClientNotFound = Error.NotFound(
        "Invoice.ClientNotFound",
        "The project's client could not be found.");

    public static readonly Error OrganizationNotFound = Error.NotFound(
        "Invoice.OrganizationNotFound",
        "Organization not found.");

    public static readonly Error NotEditable = Error.Conflict(
        "Invoice.NotEditable",
        "Only draft invoices can be edited.");

    public static readonly Error NotDeletable = Error.Conflict(
        "Invoice.NotDeletable",
        "Only draft invoices can be deleted.");

    public static readonly Error MissingContractReference = Error.Conflict(
        "Invoice.MissingContractReference",
        "Invoice is missing a valid billing contract reference.");

    public static readonly Error NumberUnavailable = Error.Conflict(
        "Invoice.NumberUnavailable",
        "Unable to allocate an invoice number.");

    public static readonly Error SessionsChanged = Error.Conflict(
        "Invoice.SessionsChanged",
        "One or more selected time sessions could not be updated for invoicing.");

    public static readonly Error SessionsUnavailable = Error.Conflict(
        "Invoice.SessionsUnavailable",
        "One or more selected time sessions could not be invoiced.");

    public static readonly Error SessionRunning = Error.Conflict(
        "Invoice.SessionRunning",
        "Running timers cannot be invoiced.");

    public static readonly Error SessionsAlreadyBilled = Error.Conflict(
        "Invoice.SessionsAlreadyBilled",
        "One or more selected time sessions are already billed.");

    public static readonly Error SessionsBilledElsewhere = Error.Conflict(
        "Invoice.SessionsBilledElsewhere",
        "One or more selected time sessions are already billed on another invoice.");

    public static readonly Error SessionsOutsideContract = Error.Conflict(
        "Invoice.SessionsOutsideContract",
        "Selected time sessions must belong to the billing contract's project.");

    public static readonly Error SessionsOverlap = Error.Conflict(
        "Invoice.SessionsOverlap",
        "Selected time sessions contain overlapping intervals.");

    public static readonly Error GenerationConflict = Error.Conflict(
        "Invoice.GenerationConflict",
        "Invoice generation failed due to a data conflict.");

    public static readonly Error UpdateConflict = Error.Conflict(
        "Invoice.UpdateConflict",
        "Invoice update failed due to a data conflict.");

    public static readonly Error DeletionConflict = Error.Conflict(
        "Invoice.DeletionConflict",
        "Invoice deletion failed due to a data conflict.");

    public static readonly Error ClientEmailMissing = Error.Conflict(
        "Invoice.ClientEmailMissing",
        "The client does not have an email address on file.");

    public static readonly Error EmailDeliveryFailed = Error.Failure(
        "Invoice.EmailDeliveryFailed",
        "Failed to send the invoice email.");

    public static string StatusInvalid => $"Invoice status must be one of: {Lifecycle.Options}.";
    public static string TakeOutOfRange => $"Take must be between {MinimumTake} and {MaximumTake}.";

    public static Error NotFound(string id)
    {
        return Error.NotFound(
            "Invoice.NotFound",
            $"Invoice with ID {id} was not found.");
    }

    public static Error StatusTransitionNotAllowed(string currentStatus, string nextStatus)
    {
        return Error.Conflict(
            "Invoice.StatusTransitionNotAllowed",
            $"Cannot transition invoice status from {currentStatus} to {nextStatus}.");
    }
}