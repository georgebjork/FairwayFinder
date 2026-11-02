using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Sinks.OpenTelemetry;

namespace FairwayFinder.ServiceDefaults;

public static class LoggingExtensions
{
    // One line per event, readable at a glance in a terminal or the Aspire console view.
    private const string DevelopmentConsoleTemplate =
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    // Same line without the timestamp: the hosting platform's log viewer already prefixes one.
    // Structured fields go to the OTLP backend, so stdout is only ever read by a person.
    private const string ProductionConsoleTemplate =
        "[{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Replaces the default logger factory with Serilog, so every <c>ILogger&lt;T&gt;</c> in the app
    /// writes through it. Levels come from the <c>Serilog</c> config section; sinks are chosen here
    /// because they depend on the environment rather than on per-key config.
    /// </summary>
    public static TBuilder ConfigureSerilog<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Deliberately no static Log.Logger / bootstrap logger: the integration tests build several
        // hosts per process, and a frozen bootstrap logger can only be frozen once. For the same
        // reason preserveStaticLogger is on: without it each host assigns its logger to Log.Logger
        // and every host's ILoggers write through whichever was assigned last, so a second test
        // host would capture (and on disposal, drop) the first host's events.
        builder.Services.AddSerilog((services, logger) =>
        {
            logger
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName);

            logger.WriteTo.Console(outputTemplate: builder.Environment.IsDevelopment()
                ? DevelopmentConsoleTemplate
                : ProductionConsoleTemplate);

            // Same switch as the trace/metric exporters: only ship when an OTLP endpoint exists.
            // The sink itself reads OTEL_EXPORTER_OTLP_ENDPOINT/_PROTOCOL/_HEADERS, OTEL_SERVICE_NAME
            // and OTEL_RESOURCE_ATTRIBUTES, which is everything Aspire sets.
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
            {
                logger.WriteTo.Logger(otlp => otlp
                    .Filter.ByIncludingOnly(ShipToOtlp)
                    .WriteTo.OpenTelemetry(options =>
                    {
                        // Default IncludedData: the body is the rendered message, so the log viewer reads
                        // like the console, and the template rides along as message_template.text to
                        // group identical events by. Substituted values ship as attributes either way.
                        options.ResourceAttributes = new Dictionary<string, object>
                        {
                            ["service.name"] = builder.Environment.ApplicationName,
                            ["deployment.environment"] = builder.Environment.EnvironmentName
                        };
                    }));
            }
        }, preserveStaticLogger: true);

        return builder;
    }

    private static readonly Func<LogEvent, bool> FromFairwayFinder = Matching.FromSource("FairwayFinder");

    private static readonly Func<LogEvent, bool> FromRequestLogging =
        Matching.FromSource("Serilog.AspNetCore.RequestLoggingMiddleware");

    /// <summary>
    /// What is worth paying the OTLP backend for: Warning and worse from anything, plus our own
    /// Information and the one summary line per request, which carries the user, route, status and
    /// timing as filterable fields. Framework Information chatter stays on the console.
    /// </summary>
    private static bool ShipToOtlp(LogEvent logEvent) =>
        logEvent.Level >= LogEventLevel.Warning || FromFairwayFinder(logEvent) || FromRequestLogging(logEvent);

    /// <summary>
    /// One summary line per request in place of ASP.NET Core's several. Register it before
    /// <c>UseExceptionHandler</c> so it sees the final status code; the exception itself is logged
    /// by the handler, so it is not logged twice here. The user is read at completion, after
    /// authentication has run further down the pipeline.
    /// </summary>
    public static WebApplication UseFairwayFinderRequestLogging(this WebApplication app)
    {
        // Blazor's framework, static-content and circuit traffic says nothing about what an admin did.
        var excludedPaths = Extensions.GetExcludedPaths(app.Configuration)
            .Concat(new PathString[] { "/_framework", "/_content", "/_blazor" })
            .ToArray();

        app.UseSerilogRequestLogging(options =>
        {
            // :l renders the strings unquoted in the JSON @m; the properties themselves are unchanged.
            options.MessageTemplate =
                "HTTP {RequestMethod:l} {RequestPath:l} responded {StatusCode} in {Elapsed:0.0} ms";

            options.GetLevel = (context, _, exception) =>
            {
                if (exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
                {
                    return LogEventLevel.Error;
                }

                // Verbose sits below every configured minimum, so excluded requests are dropped.
                if (excludedPaths.Any(excluded => context.Request.Path.StartsWithSegments(excluded)))
                {
                    return LogEventLevel.Verbose;
                }

                // Static files (css, js, images) stay available at Debug without flooding Information.
                return Path.HasExtension(context.Request.Path.Value)
                    ? LogEventLevel.Debug
                    : LogEventLevel.Information;
            };

            options.EnrichDiagnosticContext = (diagnostics, context) =>
            {
                // The route template groups every /api/rounds/{id} call together, unlike RequestPath.
                if (context.GetEndpoint() is RouteEndpoint endpoint)
                {
                    diagnostics.Set("RouteTemplate", endpoint.RoutePattern.RawText);
                }

                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId is not null)
                {
                    diagnostics.Set("UserId", userId);
                }
            };
        });

        return app;
    }

    /// <summary>
    /// Puts the signed-in user on every log event raised for the rest of the request, so a warning
    /// or error can be traced back to who caused it. Register directly after <c>UseAuthentication</c>.
    /// </summary>
    public static WebApplication UseUserLogContext(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                await next(context);
                return;
            }

            using (LogContext.PushProperty("UserId", userId))
            using (LogContext.PushProperty("UserEmail", context.User.FindFirstValue(ClaimTypes.Email)))
            {
                await next(context);
            }
        });

        return app;
    }
}
