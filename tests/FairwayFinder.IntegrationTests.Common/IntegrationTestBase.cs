using FairwayFinder.Data;
using FairwayFinder.IntegrationTests.Common.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.IntegrationTests.Common;

/// <summary>
/// Base for every integration test class. Each test starts from an empty (migrated) database with
/// no recorded email or push traffic.
/// </summary>
public abstract class IntegrationTestBase<TFactory, TEntryPoint>(TFactory factory) : IAsyncLifetime
    where TFactory : FairwayFinderFactory<TEntryPoint>
    where TEntryPoint : class
{
    protected TFactory Factory { get; } = factory;

    protected IServiceProvider Services => Factory.Services;

    protected TestData Data => Factory.Data;

    protected RecordingEmailSender Email => Factory.Email;

    protected FakeApnsClient Apns => Factory.Apns;

    public virtual Task InitializeAsync() => Factory.ResetAsync();

    public virtual Task DisposeAsync() => Task.CompletedTask;

    /// <summary>A fresh context for arranging or asserting on rows directly.</summary>
    protected ApplicationDbContext Db() =>
        Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();

    /// <summary>Resolves a service inside its own scope, as a request would.</summary>
    protected async Task<TResult> WithServiceAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    protected async Task WithServiceAsync<TService>(Func<TService, Task> action)
        where TService : notnull
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
