var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("postgres-username");
var password = builder.AddParameter("postgres-password", secret: true);

// App secrets. Missing values are prompted for in the dashboard instead of failing host startup.
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);
var resendApiKey = builder.AddParameter("resend-api-key", secret: true);
var golfCourseApiKey = builder.AddParameter("golfcourseapi-api-key", secret: true);
var apnsBundleId = builder.AddParameter("apns-bundle-id");
var apnsKeyId = builder.AddParameter("apns-key-id");
var apnsTeamId = builder.AddParameter("apns-team-id");
var apnsP8Contents = builder.AddParameter("apns-p8-contents", secret: true);

// Pinned to 17.6: Aspire 13.6 defaults to Postgres 18, which won't start on the existing PG17 data
// volume and mounts at a different path. Moving to 18 needs a dump/restore, not just a tag bump.
var postgres = builder.AddPostgres("postgres", username, password, port: 5432)
    .WithImageTag("17.6")
    .WithContainerName("fairwayfinder-postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithVolume("fairwayfinder-data", "/var/lib/postgresql/data")
    .WithPgWeb(pgweb => pgweb.WithExplicitStart(), containerName: "fairwayfinder-pgweb")
    .WithRepl();

var dbFairwayfinder = postgres.AddDatabase("fairwayfinder", "db-fairwayfinder");

builder.AddProject<Projects.FairwayFinder_Admin>("fairwayfinder-admin")
    .WithReference(dbFairwayfinder)
    .WaitFor(dbFairwayfinder)
    .WithEnvironment("Resend__ApiKey", resendApiKey)
    .WithEnvironment("GolfCourseApi__ApiKey", golfCourseApiKey)
    .WithApns(apnsBundleId, apnsKeyId, apnsTeamId, apnsP8Contents)
    // Not /health: the admin's Health Checks page is routed there and answers with a login redirect.
    .WithHttpHealthCheck("/alive")
    .WithDashboardLinks("Admin console");

builder.AddProject<Projects.FairwayFinder_Api>("fairwayfinder-api")
    .WithReference(dbFairwayfinder)
    .WaitFor(dbFairwayfinder)
    .WithEnvironment("Jwt__Secret", jwtSecret)
    .WithApns(apnsBundleId, apnsKeyId, apnsTeamId, apnsP8Contents)
    .WithHttpHealthCheck("/health")
    .WithDashboardLinks("API", ("Scalar", "/scalar/"));

builder.Build().Run();

static class ResourceBuilderExtensions
{
    public static IResourceBuilder<ProjectResource> WithApns(
        this IResourceBuilder<ProjectResource> project,
        IResourceBuilder<ParameterResource> bundleId,
        IResourceBuilder<ParameterResource> keyId,
        IResourceBuilder<ParameterResource> teamId,
        IResourceBuilder<ParameterResource> p8Contents) =>
        project
            .WithEnvironment("Apns__BundleId", bundleId)
            .WithEnvironment("Apns__KeyId", keyId)
            .WithEnvironment("Apns__TeamId", teamId)
            .WithEnvironment("Apns__P8Contents", p8Contents);

    // Labels the project's main link and adds any extra page links next to it. The main link is https
    // when the launch profile has one (plain http is then moved to the details panel), otherwise http,
    // so a run on the http launch profile still shows a link.
    public static IResourceBuilder<ProjectResource> WithDashboardLinks(
        this IResourceBuilder<ProjectResource> project,
        string displayText,
        params (string DisplayText, string Path)[] pages) =>
        project.WithUrls(context =>
        {
            var main = context.Urls.FirstOrDefault(u => u.Endpoint?.EndpointName == "https")
                       ?? context.Urls.FirstOrDefault(u => u.Endpoint?.EndpointName == "http");
            if (main is null)
            {
                return;
            }

            foreach (var url in context.Urls.Where(u => u.Endpoint is not null && u != main))
            {
                url.DisplayLocation = UrlDisplayLocation.DetailsOnly;
            }

            main.DisplayText = displayText;
            foreach (var page in pages)
            {
                context.Urls.Add(new() { Url = new Uri(new Uri(main.Url), page.Path).ToString(), DisplayText = page.DisplayText });
            }
        });
}
