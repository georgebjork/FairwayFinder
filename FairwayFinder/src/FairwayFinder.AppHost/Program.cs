var builder = DistributedApplication.CreateBuilder(args);

// Generate random username and password for the PostgreSQL database
var username = builder.AddParameter("postgres-username", "postgres");
var password = builder.AddParameter("postgres-password", "password");

var postgres = builder.AddPostgres("postgres", username, password, port: 5432).WithPgAdmin(containerName: "pgadmin")
    .WithVolume("data-fairwayfinder", "/var/lib/postgresql/data"); // ✅ correct default mount path

var db_fairwayfinder = postgres.AddDatabase("fairwayfinder", "db-fairwayfinder");


// Add web project
builder.AddProject<Projects.FairwayFinder_Web>("webapp")
    .WaitFor(postgres)
    .WithReference(db_fairwayfinder);

builder.Build().Run();