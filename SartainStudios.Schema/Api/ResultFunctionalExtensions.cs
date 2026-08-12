namespace SartainStudios.Schema.Api;

public static class ResultFunctionalExtensions
{
    public static TOut Match<TOut>(
        this Result result,
        Func<TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        return result.IsSuccess ? onSuccess() : onFailure(result.Error);
    }

    public static TOut Match<TValue, TOut>(
        this Result<TValue> result,
        Func<TValue, TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        return result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);
    }

    public static Result<TOut> Map<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> mapper)
    {
        return result.IsSuccess ? Result<TOut>.Success(mapper(result.Value)) : Result<TOut>.Failure(result.Error);
    }

    public static Result<TOut> Bind<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Result<TOut>> binder)
    {
        return result.IsSuccess ? binder(result.Value) : Result<TOut>.Failure(result.Error);
    }

    public static Result Bind<TIn>(
        this Result<TIn> result,
        Func<TIn, Result> binder)
    {
        return result.IsSuccess ? binder(result.Value) : Result.Failure(result.Error);
    }

    public static Result<TValue> Ensure<TValue>(
        this Result<TValue> result,
        Func<TValue, bool> predicate,
        Error error)
    {
        return result.IsFailure ? result : predicate(result.Value) ? result : error;
    }

    public static Result<TValue> Tap<TValue>(this Result<TValue> result, Action<TValue> action)
    {
        if (result.IsSuccess) action(result.Value);
        return result;
    }

    public static async Task<TOut> MatchAsync<TValue, TOut>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Match(onSuccess, onFailure);
    }

    public static async Task<TOut> MatchAsync<TOut>(
        this Task<Result> resultTask,
        Func<TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Match(onSuccess, onFailure);
    }
}