using System.Diagnostics.CodeAnalysis;

namespace SartainStudios.Schema.Api;

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(bool isSuccess, TValue? value, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static Result<TValue> Success(TValue value)
    {
        return new Result<TValue>(true, value, Error.None);
    }

    public new static Result<TValue> Failure(Error error)
    {
        return new Result<TValue>(false, default, error);
    }

    public static implicit operator Result<TValue>(TValue? value)
    {
        return value is not null ? Success(value) : Failure(Error.NullValue);
    }

    public static implicit operator Result<TValue>(Error error)
    {
        return Failure(error);
    }

    public bool TryGetValue([NotNullWhen(true)] out TValue? value)
    {
        value = IsSuccess ? _value : default;
        return IsSuccess;
    }
}