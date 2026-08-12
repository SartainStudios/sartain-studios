using System.Diagnostics.CodeAnalysis;

namespace SartainStudios.Schema.Api;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        switch (isSuccess)
        {
            case true when error != Error.None:
                throw new ArgumentException("A successful result cannot carry an error.", nameof(error));
            case false when error == Error.None:
                throw new ArgumentException("A failed result must carry an error.", nameof(error));
            default:
                IsSuccess = isSuccess;
                Error = error;
                break;
        }
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success()
    {
        return new Result(true, Error.None);
    }

    public static Result Failure(Error error)
    {
        return new Result(false, error);
    }

    public static Result<TValue> Success<TValue>(TValue value)
    {
        return Result<TValue>.Success(value);
    }

    public static Result<TValue> Failure<TValue>(Error error)
    {
        return Result<TValue>.Failure(error);
    }

    public static implicit operator Result(Error error)
    {
        return Failure(error);
    }

    public static Result Combine(params Result[] results)
    {
        return Array.Find(results, r => r.IsFailure) ?? Success();
    }
}