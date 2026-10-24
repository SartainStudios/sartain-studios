using SartainStudios.Schema.Api;

namespace SartainStudios.Schema.Gig;

public static class GigErrors
{
    public const string EndTimeField = "endTime";
    public const string StartTimeField = "startTime";
    public const string StartOdometerField = "startOdometer";
    public const string EndOdometerField = "endOdometer";
    public const string VehicleTypeField = "vehicleType";
    public const string EarningsField = "earnings";
    public const string ExpensesField = "expenses";
    public const string NameField = "name";
    public const string ColorField = "color";
    public const string MileageRateField = "mileageRateOverride";
    public const string WeeksField = "weeks";
    public const string TakeField = "take";
    public const int MinimumTake = 1;
    public const int MaximumTake = 200;
    public const int MinimumWeeks = 1;
    public const int MaximumWeeks = 52;
    public const int MaximumNameLength = 50;
    public const int MaximumNoteLength = 500;
    public const decimal MaximumMileageRate = 5m;

    public static readonly Error InvalidId = Error.Validation(
        "Gig.InvalidId",
        "The supplied id is not a valid identifier.");

    public static readonly Error SessionAlreadyOpen = Error.Conflict(
        "Gig.SessionAlreadyOpen",
        "A session is already running.");

    public static readonly Error NoOpenSession = Error.NotFound(
        "Gig.NoOpenSession",
        "No running session was found.");

    public static readonly Error SessionNotFound = Error.NotFound(
        "Gig.SessionNotFound",
        "Session not found.");

    public static readonly Error SessionConflict = Error.Conflict(
        "Gig.SessionConflict",
        "The session could not be saved because it changed state.");

    public static readonly Error OptionNotFound = Error.NotFound(
        "Gig.OptionNotFound",
        "The source or category was not found.");

    public static readonly Error OptionIsDefault = Error.Conflict(
        "Gig.OptionIsDefault",
        "Default sources and categories cannot be edited or deleted. You can hide them instead.");

    public static readonly Error OptionInUse = Error.Conflict(
        "Gig.OptionInUse",
        "This source or category is used by existing sessions. Hide it instead of deleting it.");

    public static readonly Error OptionNameTaken = ValidationError.FromErrors(
        (NameField, "A source or category with this name already exists."));

    public static readonly Error TakeOutOfRange = ValidationError.FromErrors(
        (TakeField, $"Take must be between {MinimumTake} and {MaximumTake}."));

    public static readonly Error WeeksOutOfRange = ValidationError.FromErrors(
        (WeeksField, $"Weeks must be between {MinimumWeeks} and {MaximumWeeks}."));

    public static readonly Error NameRequired = ValidationError.FromErrors(
        (NameField, "Name is required."));

    public static readonly Error NameTooLong = ValidationError.FromErrors(
        (NameField, $"Name must be {MaximumNameLength} characters or fewer."));

    public static readonly Error ColorInvalid = ValidationError.FromErrors(
        (ColorField, "Color must be a hex value such as #1976D2."));

    public static readonly Error MileageRateInvalid = ValidationError.FromErrors(
        (MileageRateField, $"Mileage rate must be greater than 0 and at most {MaximumMileageRate:0.00} per mile."));
}