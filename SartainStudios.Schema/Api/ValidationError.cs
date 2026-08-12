namespace SartainStudios.Schema.Api;

public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors)
    : Error("General.Validation", "One or more validation errors occurred.", ErrorType.Validation)
{
    public static ValidationError FromErrors(params (string Field, string Message)[] errors)
    {
        return new ValidationError(errors
            .GroupBy(error => error.Field)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray())
        );
    }
}