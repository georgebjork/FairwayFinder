using FairwayFinder.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Api.IntegrationTests.Conventions;

/// <summary>
/// What the API does with only its committed configuration, outside Development. Production
/// supplies secrets through the environment, so the defaults it inherits must already be the
/// production ones and a missing secret must stop the host rather than fail a golfer later.
/// </summary>
public class ProductionConfigurationTests(ApiFactory factory) : ApiTestBase(factory)
{
    private IConfiguration CommittedSettings(params string[] files)
    {
        var contentRoot = Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        var builder = new ConfigurationBuilder().SetBasePath(contentRoot);
        foreach (var file in files) builder.AddJsonFile(file, optional: false);
        return builder.Build();
    }

    [Fact]
    public void Push_goes_to_the_production_apns_server_unless_development_says_otherwise()
    {
        Assert.False(CommittedSettings("appsettings.json").GetValue<bool>("Apns:UseSandbox"));
        Assert.True(CommittedSettings("appsettings.json", "appsettings.Development.json").GetValue<bool>("Apns:UseSandbox"));
    }

    [Fact]
    public void Host_refuses_to_start_outside_development_without_a_resend_api_key()
    {
        using var withoutKey = Factory.WithWebHostBuilder(b => b.UseSetting("Resend:ApiKey", ""));

        var ex = Assert.ThrowsAny<Exception>(() => withoutKey.Services);
        Assert.Contains("Resend:ApiKey", ex.ToString());
    }
}
