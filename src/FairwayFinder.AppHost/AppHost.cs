var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("postgres-username");
var password = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", username, password, port: 5432)
    .WithContainerName("fairwayfinder-postgres")
    .WithPgWeb(containerName: "fairwayfinder-pgweb")
    .WithVolume("fairwayfinder-data", "/var/lib/postgresql/data");

var dbFairwayfinder = postgres.AddDatabase("fairwayfinder", "db-fairwayfinder");

builder.AddProject<Projects.FairwayFinder_Web>("fairwayfinder-web")
    .WithReference(dbFairwayfinder)
    .WaitFor(dbFairwayfinder);

builder.Build().Run();