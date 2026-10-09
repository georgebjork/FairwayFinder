using dotAPNS;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.IntegrationTests.Common.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FairwayFinder.IntegrationTests.Common;

/// <summary>
/// Boots a real FairwayFinder host (Api or Admin) in-process against its own Postgres container.
/// Shared by every test class in the assembly through an xUnit collection fixture; tests reset
/// the data, not the host, between runs.
/// </summary>
/// <remarks>
/// Only the edges of the system are faked: outgoing email and Apple's push gateway. Everything
/// between the HTTP request and the database row is the production code path.
/// </remarks>
public abstract class FairwayFinderFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>, IAsyncLifetime
    where TEntryPoint : class
{
    public PostgresFixture Postgres { get; } = new();

    public RecordingEmailSender Email { get; } = new();

    public FakeApnsClient Apns { get; } = new();

    /// <summary>Canned responses for third-party HTTP APIs, for hosts that wire it into their clients.</summary>
    public StubHttpHandler ExternalHttp { get; } = new();

    /// <summary>Extra configuration layered over <see cref="TestSettings"/>.</summary>
    protected virtual IReadOnlyDictionary<string, string?> SettingOverrides { get; } =
        new Dictionary<string, string?>();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Postgres.InitializeAsync();
        await PrepareDatabaseAsync();
        await ResetAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await Postgres.DisposeAsync();
    }

    /// <summary>
    /// Brings the schema up to date before the first test. Runs after the container starts and
    /// before the first reset, which needs the migrated table graph.
    /// </summary>
    protected abstract Task PrepareDatabaseAsync();

    /// <summary>Wipes test data and the recorded side effects. Called before every test.</summary>
    public async Task ResetAsync()
    {
        await Postgres.ResetAsync();
        Email.Clear();
        Apns.Clear();
        ExternalHttp.Clear();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseTestSettings(Postgres.ConnectionString, SettingOverrides);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);

            services.RemoveAll<IApnsClient>();
            services.AddSingleton<IApnsClient>(Apns);

            ConfigureTestServices(services);
        });
    }

    /// <summary>Host-specific service overrides, applied after the shared fakes.</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    public TestData Data => new(Services);
}
