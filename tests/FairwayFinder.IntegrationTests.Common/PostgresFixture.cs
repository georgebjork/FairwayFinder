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

    private const string LogsDatabase = "db-fairwayfinder-logs";

    private Respawner? _respawner;
    private Respawner? _logsRespawner;

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>The logs database, in the same container as in production it is on the same server.</summary>
    public string LogsConnectionString => new NpgsqlConnectionStringBuilder(ConnectionString)
    {
        Database = LogsDatabase
    }.ConnectionString;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Created up front rather than by MigrateAsync: the API host never creates databases, and
        // the API's tests migrate the schema themselves.
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{LogsDatabase}\"", connection);
        await create.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Deletes every test-written row in both databases. The schemas must already be migrated: the
    /// respawners read the table graph the first time they run.
    /// </summary>
    public async Task ResetAsync()
    {
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();

            _respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = TablesToKeep,
            });

            await _respawner.ResetAsync(connection);
        }

        await using (var logs = new NpgsqlConnection(LogsConnectionString))
        {
            await logs.OpenAsync();

            _logsRespawner ??= await Respawner.CreateAsync(logs, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = ["__EFMigrationsHistory"],
            });

            await _logsRespawner.ResetAsync(logs);
        }
    }
}
