using DotNet.Testcontainers.Builders;
using FairwayFinder.Core.Settings;
using FairwayFinder.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace FairwayFinder.Tests.Integration;

public class ConfigureFactory : WebApplicationFactory<IWebMarker>, IAsyncLifetime
{
    // This will spin up a docker container with a db inside for testing.
    private readonly PostgreSqlContainer _dbContainer =
        new PostgreSqlBuilder()
            .WithDatabase("FairwayFinder")
            .WithUsername("postgres")
            .WithPassword("password")
            .WithPortBinding(5555, 5432) // (External Port, Internal Port)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();


    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // This assumes your Postgres container exposes the port to the host machine,
            // and you want to build a connection string dynamically based on that.
            const string connectionString = "Host=localhost;Port=5555;Database=FairwayFinder;Username=postgres;Password=password";
        
            // Override the SQL_CONNECTION_NAME setting with the new connection string
            var testConfig = new Dictionary<string, string>
            {
                { $"ConnectionStrings:{ApplicationSettings.SQL_CONNECTION_NAME}", connectionString }
            };

            config.AddInMemoryCollection(testConfig!);
        });
    }
    
    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
    }
}