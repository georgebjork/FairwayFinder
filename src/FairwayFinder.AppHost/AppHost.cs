var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("postgres-username");
var password = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", username, password)
    .WithPgWeb()
    .AddDatabase("fairwayfinder-db");

builder.AddProject<Projects.FairwayFinder_Web>("fairwayfinder-web")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.Build().Run();