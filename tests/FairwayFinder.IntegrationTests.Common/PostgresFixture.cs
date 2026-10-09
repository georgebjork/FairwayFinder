using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace FairwayFinder.IntegrationTests.Common;

/// <summary>
/// One throwaway Postgres container per test assembly. Integration tests run against the real
/// database engine because several behaviours only exist there: the filtered unique indexes that
/// guard round entry and game join codes, <c>ILIKE</c> search, <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>,
/// and the <c>PostgresException</c> unique-violation retries.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Rows the schema itself owns: migration history, migration-seeded lookups, the roles every
    // host expects to exist, and the shared Data Protection key ring. Everything else is wiped
    // between tests.
    private static readonly Respawn.Graph.Table[] TablesToKeep =
    [
        "__EFMigrationsHistory",
        "miss_type",
        "AspNetRoles",
        "data_protection_key",
    ];

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("db-fairwayfinder")
        .Build();

    private Respawner? _respawner;

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Deletes every test-written row. The schema must already be migrated: the respawner reads
    /// the table graph the first time it runs.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = TablesToKeep,
        });

        await _respawner.ResetAsync(connection);
    }
}
