using MongoDB.Bson;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Gig;

public static class GigSessionValidation
{
    public const int MaximumEntries = 50;
    public const decimal MaximumAmount = 1_000_000m;
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(1);

    public static Result<ValidatedGigSession> Validate(
        DateTime startTime,
        DateTime? endTime,
        decimal? startOdometer,
        decimal? endOdometer,
        string? vehicleType,
        string? notes,
        IReadOnlyList<EarningEntry>? earnings,
        IReadOnlyList<ExpenseEntry>? expenses,
        DateTime now)
    {
        var errors = new List<(string Field, string Message)>();
        if (startTime.Kind != DateTimeKind.Utc)
            errors.Add((GigErrors.StartTimeField, "Start time must be in UTC."));
        else if (startTime > now + ClockTolerance)
            errors.Add((GigErrors.StartTimeField, "Start time cannot be in the future."));
        if (endTime is { } end)
        {
            if (end.Kind != DateTimeKind.Utc)
                errors.Add((GigErrors.EndTimeField, "End time must be in UTC."));
            else if (end > now + ClockTolerance)
                errors.Add((GigErrors.EndTimeField, "End time cannot be in the future."));
            else if (startTime.Kind == DateTimeKind.Utc && end < startTime)
                errors.Add((GigErrors.EndTimeField, "End time cannot be earlier than the start time."));
        }

        if (startOdometer is < 0)
            errors.Add((GigErrors.StartOdometerField, "Start odometer cannot be negative."));
        if (endOdometer is < 0)
            errors.Add((GigErrors.EndOdometerField, "End odometer cannot be negative."));
        if (startOdometer is { } startReading && endOdometer is { } endReading &&
            startReading >= 0 && endReading >= 0 && endReading < startReading)
            errors.Add((GigErrors.EndOdometerField, "End odometer cannot be below the start odometer."));
        if (!VehicleTypes.TryNormalize(vehicleType, out var normalizedVehicleType))
            errors.Add((GigErrors.VehicleTypeField, "Choose a valid vehicle type."));
        var trimmedNotes = notes?.Trim() ?? string.Empty;
        if (trimmedNotes.Length > GigErrors.MaximumNoteLength)
            errors.Add(("notes", $"Notes must be {GigErrors.MaximumNoteLength} characters or fewer."));
        var earningEntries = ValidateEarnings(earnings, errors);
        var expenseEntries = ValidateExpenses(expenses, errors);
        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);
        return new ValidatedGigSession(
            startTime,
            endTime,
            startOdometer,
            endOdometer,
            normalizedVehicleType,
            trimmedNotes,
            earningEntries,
            expenseEntries);
    }

    private static List<GigEarningEntry> ValidateEarnings(
        IReadOnlyList<EarningEntry>? earnings,
        List<(string Field, string Message)> errors)
    {
        var result = new List<GigEarningEntry>();
        if (earnings is null) return result;
        if (earnings.Count > MaximumEntries)
        {
            errors.Add((GigErrors.EarningsField, $"No more than {MaximumEntries} earnings are allowed."));
            return result;
        }

        for (var index = 0; index < earnings.Count; index++)
        {
            var entry = earnings[index];
            var field = $"{GigErrors.EarningsField}[{index}]";
            if (!ObjectId.TryParse(entry.SourceId, out var sourceId))
                errors.Add((field, "Choose a valid source."));
            if (!IsValidAmount(entry.Amount, out var amountError))
                errors.Add((field, amountError));
            if (entry.Note?.Trim().Length > GigErrors.MaximumNoteLength)
                errors.Add((field, $"Notes must be {GigErrors.MaximumNoteLength} characters or fewer."));
            result.Add(new GigEarningEntry
            {
                SourceId = sourceId,
                Amount = GigCalculator.Round(entry.Amount),
                Note = entry.Note?.Trim() ?? string.Empty
            });
        }

        return result;
    }

    private static List<GigExpenseEntry> ValidateExpenses(
        IReadOnlyList<ExpenseEntry>? expenses,
        List<(string Field, string Message)> errors)
    {
        var result = new List<GigExpenseEntry>();
        if (expenses is null) return result;
        if (expenses.Count > MaximumEntries)
        {
            errors.Add((GigErrors.ExpensesField, $"No more than {MaximumEntries} expenses are allowed."));
            return result;
        }

        for (var index = 0; index < expenses.Count; index++)
        {
            var entry = expenses[index];
            var field = $"{GigErrors.ExpensesField}[{index}]";
            if (!ObjectId.TryParse(entry.CategoryId, out var categoryId))
                errors.Add((field, "Choose a valid category."));
            if (!IsValidAmount(entry.Amount, out var amountError))
                errors.Add((field, amountError));
            if (entry.Note?.Trim().Length > GigErrors.MaximumNoteLength)
                errors.Add((field, $"Notes must be {GigErrors.MaximumNoteLength} characters or fewer."));
            result.Add(new GigExpenseEntry
            {
                CategoryId = categoryId,
                Amount = GigCalculator.Round(entry.Amount),
                Note = entry.Note?.Trim() ?? string.Empty
            });
        }

        return result;
    }

    private static bool IsValidAmount(decimal amount, out string error)
    {
        if (amount < 0)
        {
            error = "Amounts cannot be negative.";
            return false;
        }

        if (amount > MaximumAmount)
        {
            error = $"Amounts cannot exceed {MaximumAmount:N0}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public sealed record ValidatedGigSession(
        DateTime StartTime,
        DateTime? EndTime,
        decimal? StartOdometer,
        decimal? EndOdometer,
        string VehicleType,
        string Notes,
        List<GigEarningEntry> Earnings,
        List<GigExpenseEntry> Expenses);
}