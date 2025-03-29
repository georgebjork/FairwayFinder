var builder = DistributedApplication.CreateBuilder(args);

// postgres username and password
var username = builder.AddParameter("postgres-username", "postgres");
var password = builder.AddParameter("postgres-password", "password");

// Add postgres with pg web
var postgres = builder.AddPostgres("postgres", username, password, port: 5432).WithPgWeb(containerName: "pgweb")
    .WithVolume("data-fairwayfinder", "/var/lib/postgresql/data");

// Add database
var db_fairwayfinder = postgres.AddDatabase("fairwayfinder", "db-fairwayfinder");

// Add web project
builder.AddProject<Projects.FairwayFinder_Web>("webapp")
    .WaitFor(postgres)
    .WithReference(db_fairwayfinder);

builder.Build().Run();