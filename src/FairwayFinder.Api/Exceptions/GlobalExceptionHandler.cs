using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace FairwayFinder.Api.Exceptions;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An exception occurred: {Message}", exception.Message);

        var result = exception switch
        {
            HttpResponseException httpEx => Results.Problem(
                detail: httpEx.Message,
                statusCode: httpEx.StatusCode),

            ValidationException valEx => Results.ValidationProblem(
                detail: valEx.Message,
                errors: valEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    )),

            _ => Results.Problem(
                detail: "An unexpected error occurred",
                statusCode: StatusCodes.Status500InternalServerError)
        };

        await result.ExecuteAsync(httpContext);
        return true;
    }
}
