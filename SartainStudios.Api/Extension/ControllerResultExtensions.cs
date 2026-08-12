using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SartainStudios.Schema.Api;

namespace SartainStudios.Api.Extension;

public static class ControllerResultExtensions
{
    public static ActionResult<TValue> ToActionResult<TValue>(
        this Result<TValue> result,
        ControllerBase controller,
        Func<TValue, ActionResult<TValue>>? onSuccess = null)
    {
        return result.IsSuccess
            ? onSuccess?.Invoke(result.Value) ?? controller.Ok(result.Value)
            : ((Result)result).ToActionResult(controller);
    }

    public static async Task<ActionResult<TValue>> ToActionResultAsync<TValue>(
        this Task<Result<TValue>> resultTask,
        ControllerBase controller,
        Func<TValue, ActionResult<TValue>>? onSuccess = null)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.ToActionResult(controller, onSuccess);
    }

    public static async Task<ActionResult> ToActionResultAsync(
        this Task<Result> resultTask,
        ControllerBase controller,
        Func<ActionResult> onSuccess)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.ToActionResult(controller, onSuccess);
    }

    private static ActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("Cannot convert a successful result to a problem response.");

        if (result.Error is not ValidationError validationError)
            return WithErrorCode(
                controller.Problem(
                    title: GetTitle(result.Error.Type),
                    detail: result.Error.Description,
                    statusCode: GetStatusCode(result.Error.Type)),
                result.Error.Code);

        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in validationError.Errors)
        foreach (var message in messages)
            modelState.AddModelError(field, message);

        return WithErrorCode(controller.ValidationProblem(modelState), validationError.Code);
    }

    private static ActionResult WithErrorCode(ActionResult actionResult, string code)
    {
        if (actionResult is ObjectResult { Value: ProblemDetails problemDetails })
            problemDetails.Extensions["code"] = code;

        return actionResult;
    }

    private static ActionResult ToActionResult(
        this Result result,
        ControllerBase controller,
        Func<ActionResult> onSuccess)
    {
        return result.IsSuccess ? onSuccess() : result.ToActionResult(controller);
    }

    private static int GetStatusCode(ErrorType type)
    {
        return type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static string GetTitle(ErrorType type)
    {
        return type switch
        {
            ErrorType.Validation => "Validation failed",
            ErrorType.Unauthorized => "Unauthorized",
            ErrorType.Forbidden => "Forbidden",
            ErrorType.NotFound => "Resource not found",
            ErrorType.Conflict => "Conflict",
            _ => "An unexpected error occurred"
        };
    }
}