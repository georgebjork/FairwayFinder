using FairwayFinder.Data;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Tests.Helpers;

/// <summary>
/// Test double for <see cref="IDbContextFactory{ApplicationDbContext}"/> backed by the EF Core
/// in-memory provider. Every created context targets the same named database so writes from one
/// context are visible to the next — mirroring how the real factory hands out short-lived contexts
/// over a shared store.
/// </summary>
public sealed class InMemoryDbContextFactory : IDbContextFactory<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public InMemoryDbContextFactory(string databaseName)
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;
    }

    public ApplicationDbContext CreateDbContext() => new(_options);

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
