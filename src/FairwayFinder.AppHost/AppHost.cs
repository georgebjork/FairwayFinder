var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("postgres-username");
var password = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", username, password, port: 5432)
    .WithContainerName("fairwayfinder-postgres")
    .WithPgWeb(containerName: "fairwayfinder-pgweb")
    .WithVolume("fairwayfinder-data", "/var/lib/postgresql/data");

var dbFairwayfinder = postgres.AddDatabase("fairwayfinder", "db-fairwayfinder");

// Grafana Cloud OTLP — only override the Aspire dashboard endpoint when both values are set.
// Populate via user-secrets or env (Parameters:grafana-otlp-endpoint / headers) for production.
var grafanaOtlpEndpoint = builder.Configuration["Parameters:grafana-otlp-endpoint"];
var grafanaOtlpHeaders  = builder.Configuration["Parameters:grafana-otlp-headers"];
var useGrafanaCloud = !string.IsNullOrWhiteSpace(grafanaOtlpEndpoint) &&
                      !string.IsNullOrWhiteSpace(grafanaOtlpHeaders);

void AddGrafanaCloudEnv(IResourceBuilder<ProjectResource> project)
{
    if (!useGrafanaCloud) return;
    project
        .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", grafanaOtlpEndpoint)
        .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS",  grafanaOtlpHeaders)
        .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
}

AddGrafanaCloudEnv(
    builder.AddProject<Projects.FairwayFinder_Web>("fairwayfinder-web")
        .WithReference(dbFairwayfinder)
        .WaitFor(dbFairwayfinder));

AddGrafanaCloudEnv(
    builder.AddProject<Projects.FairwayFinder_Api>("fairwayfinder-api")
        .WithReference(dbFairwayfinder)
        .WaitFor(dbFairwayfinder));


builder.Build().Run();