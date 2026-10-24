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
        var statusCode = exception switch
        {
            HttpResponseException httpEx => httpEx.StatusCode,
            BadHttpRequestException badReq => badReq.StatusCode,
            ValidationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        // Only a server fault is an error. A 4xx is the client's mistake answered as designed (a
        // missing round, a failed validation), so it stays out of the Warning+ error log and keeps
        // no stack trace; the request itself is still in the request log.
        if (statusCode >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "An exception occurred: {Message}", exception.Message);
        else
            logger.LogInformation("Request rejected with {StatusCode}: {Message}", statusCode, exception.Message);

        var result = exception switch
        {
            HttpResponseException httpEx => Results.Problem(
                detail: httpEx.Message,
                statusCode: httpEx.StatusCode),

            // Thrown by model binding before a handler ever runs — a required query parameter
            // missing, or a route value that will not parse. It already carries the right status
            // (400); without this case it falls to the catch-all below and a malformed request
            // reads as a server fault.
            BadHttpRequestException badReq => Results.Problem(
                detail: badReq.Message,
                statusCode: badReq.StatusCode),

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
