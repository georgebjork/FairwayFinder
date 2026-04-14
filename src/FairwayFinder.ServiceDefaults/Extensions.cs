using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        "FairwayFinder.Agents"
    ];

    public static readonly string[] FairwayFinderSources =
    [
        "FairwayFinder.Rounds",
        "FairwayFinder.Stats",
        "FairwayFinder.Imports",
        "FairwayFinder.Agents"
    ];

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Bridge OpenTelemetry:Otlp appsettings → OTEL_EXPORTER_OTLP_* env vars for the OTel SDK.
        // In dev Aspire injects these env vars directly (dashboard endpoint) and we don't override;
        // in prod the appsettings values flow through when no env var is set.
        BridgeOtlpConfigToEnvironment(builder.Configuration);

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

    private static void BridgeOtlpConfigToEnvironment(IConfiguration configuration)
    {
        SetEnvIfMissing("OTEL_EXPORTER_OTLP_ENDPOINT", configuration["OpenTelemetry:Otlp:Endpoint"]);
        SetEnvIfMissing("OTEL_EXPORTER_OTLP_HEADERS",  configuration["OpenTelemetry:Otlp:Headers"]);
        SetEnvIfMissing("OTEL_EXPORTER_OTLP_PROTOCOL", configuration["OpenTelemetry:Otlp:Protocol"]);
    }

    private static void SetEnvIfMissing(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))) return;
        Environment.SetEnvironmentVariable(name, value);
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("deployment.environment", builder.Environment.EnvironmentName)
                }))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Net.Http",
                        "Npgsql");

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
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath) &&
                            !context.Request.Path.StartsWithSegments(AlivenessEndpointPath);
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource("Npgsql");

                foreach (var source in FairwayFinderSources)
                {
                    tracing.AddSource(source);
                }
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Read the env var directly — BridgeOtlpConfigToEnvironment has already populated it from appsettings
        // when no host-level env var was present. IConfiguration's env-var source snapshot is stale at this point.
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
