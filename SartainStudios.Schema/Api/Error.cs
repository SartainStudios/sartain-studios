namespace SartainStudios.Schema.Api;

public record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None =
        new(string.Empty, string.Empty, ErrorType.None);

    public static readonly Error NullValue =
        new("General.NullValue", "A null value was provided.", ErrorType.Failure);

    public static Error NotFound(string code, string description)
    {
        return new Error(code, description, ErrorType.NotFound);
    }

    public static Error Validation(string code, string description)
    {
        return new Error(code, description, ErrorType.Validation);
    }

    public static Error Conflict(string code, string description)
    {
        return new Error(code, description, ErrorType.Conflict);
    }

    public static Error Unauthorized(string code, string description)
    {
        return new Error(code, description, ErrorType.Unauthorized);
    }

    public static Error Forbidden(string code, string description)
    {
        return new Error(code, description, ErrorType.Forbidden);
    }

    public static Error Failure(string code, string description)
    {
        return new Error(code, description, ErrorType.Failure);
    }
}