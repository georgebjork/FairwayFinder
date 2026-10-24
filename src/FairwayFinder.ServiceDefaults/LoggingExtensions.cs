using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace FairwayFinder.ServiceDefaults;

public static class LoggingExtensions
{
    // One line per event, readable at a glance in a terminal or the Aspire console view.
    private const string DevelopmentConsoleTemplate =
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Replaces the default logger factory with Serilog, so every <c>ILogger&lt;T&gt;</c> in the app
    /// writes through it. Levels come from the <c>Serilog</c> config section; sinks are chosen here
    /// because they depend on the environment rather than on per-key config.
    /// </summary>
    public static TBuilder ConfigureSerilog<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Deliberately no static Log.Logger / bootstrap logger: the integration tests build several
        // hosts per process, and a frozen bootstrap logger can only be frozen once.
        builder.Services.AddSerilog((services, logger) =>
        {
            logger
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName);

            if (builder.Environment.IsDevelopment())
            {
                logger.WriteTo.Console(outputTemplate: DevelopmentConsoleTemplate);
            }
            else
            {
                // Newline-delimited JSON (CLEF) on stdout: @t, @m, @i, @l, @x, @tr, @sp plus every
                // template property as its own field, so a log shipper can index without parsing.
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }

            // Same switch as the trace/metric exporters: only ship when an OTLP endpoint exists.
            // The sink itself reads OTEL_EXPORTER_OTLP_ENDPOINT/_PROTOCOL/_HEADERS, OTEL_SERVICE_NAME
            // and OTEL_RESOURCE_ATTRIBUTES, which is everything Aspire sets.
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
            {
                logger.WriteTo.OpenTelemetry(options =>
                {
                    // The template is the body rather than the rendered text, and not repeated as a
                    // message_template.text attribute. Substituted values still ship as attributes,
                    // and identical events group cleanly instead of fanning out on interpolated values.
                    options.IncludedData = (options.IncludedData | IncludedData.TemplateBody)
                                           & ~IncludedData.MessageTemplateTextAttribute;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = builder.Environment.ApplicationName,
                        ["deployment.environment"] = builder.Environment.EnvironmentName
                    };
                });
            }
        });

        return builder;
    }

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
}
