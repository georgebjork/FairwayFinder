using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FairwayFinder.ServiceDefaults;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static readonly string[] FairwayFinderMeters =
    [
        "FairwayFinder.Rounds",
        "FairwayFinder.Stats",
        "FairwayFinder.Imports",
        "FairwayFinder.Email",
        "FairwayFinder.Agents",
        "FairwayFinder.Games"
    ];

    public static readonly string[] FairwayFinderSources =
    [
        "FairwayFinder.Rounds",
        "FairwayFinder.Stats",
        "FairwayFinder.Imports",
        "FairwayFinder.Agents",
        "FairwayFinder.Games"
    ];

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureSerilog();
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var excludedPaths = GetExcludedPaths(builder.Configuration);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: builder.Environment.ApplicationName)
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("deployment.environment", builder.Environment.EnvironmentName)
                }))
            .WithMetrics(metrics =>
            {
                // Deliberately NOT AddAspNetCoreInstrumentation()/AddHttpClientInstrumentation():
                // those subscribe to a whole family of meters (Routing, Diagnostics, Kestrel,
                // RateLimiting, Http.Connections, SignalR), none of which we query. Naming the
                // meters we actually want is both smaller and explicit about what ships.
                metrics
                    .AddMeter(
                        "Microsoft.AspNetCore.Hosting",  // http.server.* — route latency, in-flight requests
                        "System.Net.Http",               // http.client.* — outbound latency, pool and queue
                        "Npgsql");                       // db.client.* — pool health, command duration

                foreach (var meter in FairwayFinderMeters)
                {
                    metrics.AddMeter(meter);
                }
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = context =>
                        {
                            foreach (var excluded in excludedPaths)
                            {
                                if (context.Request.Path.StartsWithSegments(excluded))
                                {
                                    return false;
                                }
                            }

                            return true;
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddNpgsql();

                foreach (var source in FairwayFinderSources)
                {
                    tracing.AddSource(source);
                }
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>
    /// Paths not worth a span or a request log line. The health endpoints are always excluded; the
    /// rest are read from the API's request-log exclusions so the three cannot drift — if a path
    /// isn't worth a row in ApiRequestLog it isn't worth paying the exporter for either. Admin has
    /// no such section and simply falls back to the health endpoints.
    /// </summary>
    internal static PathString[] GetExcludedPaths(IConfiguration configuration) =>
        new[] { HealthEndpointPath, AlivenessEndpointPath }
            .Concat(configuration.GetSection("RequestLogging:ExcludedPathPrefixes").Get<string[]>() ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(prefix => new PathString(prefix))
            .ToArray();

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);

            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
