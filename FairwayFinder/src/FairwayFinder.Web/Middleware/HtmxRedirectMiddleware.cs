namespace FairwayFinder.Web.Middleware;

public class HtmxRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HtmxRedirectMiddleware> _logger;

    public HtmxRedirectMiddleware(RequestDelegate next, ILogger<HtmxRedirectMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context); // Let controller execute

        // Only for HTMX requests
        if (context.Request.Headers.ContainsKey("HX-Request") && IsRedirectStatus(context.Response.StatusCode))
        {
            // Pull redirect location from standard header
            var location = context.Response.Headers["Location"].FirstOrDefault();

            if (!string.IsNullOrEmpty(location))
            {
                // Clear server-side redirect
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.Headers.Remove("Location");

                // Set HTMX redirect header
                context.Response.Headers["HX-Redirect"] = location;

                _logger.LogInformation("Converted server redirect to HX-Redirect: {Location}", location);
            }
        }
    }

    private bool IsRedirectStatus(int statusCode)
    {
        return statusCode is StatusCodes.Status301MovedPermanently
            or StatusCodes.Status302Found
            or StatusCodes.Status307TemporaryRedirect
            or StatusCodes.Status308PermanentRedirect;
    }
}
