# Database

FairwayFinder stores everything in PostgreSQL through EF Core, split across two databases with one `DbContext` each. This page covers how the contexts are set up, the conventions every table follows (naming, audit stamps, soft deletes, dates), how concurrency is handled with unique indexes, and how to add a migration. For the table-by-table reference see [schema.md](schema.md).

## Two databases, two contexts

| Connection string | Database name (dev) | Context | Holds | Migrations folder |
|---|---|---|---|---|
| `fairwayfinder` | `db-fairwayfinder` | `ApplicationDbContext` | Identity tables, all golf data, the Data Protection key ring | `src/FairwayFinder.Data/Migrations/` |
| `fairwayfinderlogs` | `db-fairwayfinder-logs` | `LogDbContext` | `app_log`, `api_request_log` | `src/FairwayFinder.Data/Migrations/Logs/` |

Both contexts live in `src/FairwayFinder.Data` (`ApplicationDbContext.cs`, `LogDbContext.cs`). There are no repositories; services in `FairwayFinder.Features` inject an `IDbContextFactory<T>` and query EF directly (see [../architecture/overview.md](../architecture/overview.md)).

**Why two databases.** Diagnostics are high-volume and purged on a retention schedule. Keeping them in a separate database means log growth, vacuuming and `ExecuteDelete` purges never touch golf data, and a backup or restore of golf data does not drag logs along. In production both databases sit on the same Postgres server. The split happened in commit `e88b56f`: migration `20261024050141_MoveApiRequestLogToLogsDb` drops `api_request_log` from the main database and `Migrations/Logs/20261024050151_InitialLogs` recreates it (plus `app_log`) in the logs database. Existing request-log rows were not copied across.

Rule: diagnostics go in `LogDbContext`, never in `ApplicationDbContext`.

### Registration

Each host registers both contexts as factories in its `Program.cs` (`src/FairwayFinder.Admin/Program.cs`, `src/FairwayFinder.Api/Program.cs`):

```csharp
builder.Services.AddDbContextFactory<ApplicationDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContextFactory<LogDbContext>(o => o.UseNpgsql(logsConnectionString));
```

Both throw at startup if either connection string is missing. In local dev the Aspire AppHost (`src/FairwayFinder.AppHost`) starts a Postgres 17 container on port 5432 and creates both databases (`AddDatabase("fairwayfinder", "db-fairwayfinder")`, `AddDatabase("fairwayfinderlogs", "db-fairwayfinder-logs")`), injecting the connection strings into both hosts. The `appsettings.json` of each host also carries `localhost` fallbacks. See [../getting-started.md](../getting-started.md) and [../architecture/hosting-and-configuration.md](../architecture/hosting-and-configuration.md).

### Context-specific behaviour

- `ApplicationDbContext` extends `IdentityDbContext<ApplicationUser>` and implements `IDataProtectionKeyContext`. Its `OnConfiguring` adds `AuditStampInterceptor.Instance`. Doing this in the context rather than in DI means the interceptor is present however the options were built, including the hand-built InMemory options in `FairwayFinder.Features.Tests`.
- `LogDbContext` uses `NullLoggerFactory` in `OnConfiguring`. The Serilog database sink writes through this context, so if EF logged a failed write at Error the sink would try to persist that, fail again, and loop. Keep it silent. Column lengths for `app_log` are in the `AppLogColumns` constants class next to it, which the sink uses to truncate values. See [../operations/logging-and-observability.md](../operations/logging-and-observability.md).

### Data Protection keys

The ASP.NET Core Data Protection key ring is stored in the `data_protection_key` table of the main database (migration `20260925060324_AddDataProtectionKeys`). It is configured once, in `RegisterFeatureServices` (`src/FairwayFinder.Features/ServiceRegistration.cs`), which both hosts call:

```csharp
services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("FairwayFinder");
```

Admin and Api are separate deployments that both mint and validate Identity tokens. A password-reset token minted by one has to be decrypted by the other. With a container-local key ring that fails (and the ring is lost on every redeploy). `SetApplicationName` matters as much as the shared ring: the default discriminator comes from the content root path, which differs between the hosts. Changing the `"FairwayFinder"` string invalidates every outstanding reset link and admin auth cookie.

`DataProtectionKey` is a framework type, mapped in `ApplicationDbContext` only to give it snake_case names. It is not `IAuditable`.

## Migrations at startup

The Admin host owns the schema. On startup `src/FairwayFinder.Admin/Program.cs` creates a scope and runs `ApplicationStartupService` (`src/FairwayFinder.Admin/Services/ApplicationStartupService.cs`):

1. `RunMigrationsAsync` calls `Database.MigrateAsync()` on `ApplicationDbContext`, then on `LogDbContext`. `MigrateAsync` creates the logs database if it does not exist, which needs the `CREATEDB` privilege. Without it, create `db-fairwayfinder-logs` by hand once.
2. `EnsureRolesExistAsync` creates the `Admin` and `User` roles (`ApplicationRoles` in `src/FairwayFinder.Identity`).
3. `SeedDefaultUserAsync` creates an admin user from the `SeedUser` settings if configured and absent, with `EmailConfirmed = true` (there is no confirmation flow in the console) and a `user_profile` row.

The API never migrates and assumes the schema is current. Deploy Admin first when a release contains a migration. In Development the Admin also maps `UseMigrationsEndPoint()`.

Data seeded by migrations rather than code: the `miss_type` lookup rows (`Left`, `Right`, `Short`, `Long`, and `None` with id 999).

## Naming conventions

| Thing | Convention | Example |
|---|---|---|
| Domain tables | snake_case singular, set explicitly with `ToTable` | `round`, `hole_stats`, `game_participant` |
| Domain columns | snake_case, set explicitly with `HasColumnName` on every property | `date_played`, `is_complete` |
| Primary keys | `<table>_id`, `bigint` identity; constraint `<table>_pkey` | `round_id`, `round_pkey` |
| Named indexes | `ix_<table>_<purpose>`, set with `HasDatabaseName` | `ix_round_user_id_active` |
| Convention indexes on FKs | EF default, PascalCase prefix | `IX_hole_teebox_id` |
| Raw-SQL constraints | `ck_` for checks, `ux_` for unique indexes | `ck_friendship_no_self`, `ux_friendship_pair_active` |
| Identity tables | ASP.NET Core defaults, PascalCase | `AspNetUsers.FirstName` |

There is no global naming convention plugin; every mapping is explicit in `OnModelCreating`. When adding an entity, map every column by hand to match. Exceptions to know about:

- `AspNetUsers` keeps PascalCase columns (`FirstName`, `SgBaselineLevel`, `IsSearchHidden`, `CreatedOn`) except `preferred_tees`, which is mapped to snake_case.
- `hole_stats` and `round_stats` are plural, with keys `hole_stats_id` / `round_stats_id`. Entity names are singular (`HoleStat`, `RoundStat`).
- `user_invitation` uses `id` (an `int`) as its key.
- `MissType.MissType1` maps to column `miss_type` (a scaffolding artefact from when the class name collided with the property).
- `refresh_token` and `user_device` use `created_at`/`updated_at` instead of `created_on`/`updated_on` and are not `IAuditable`.

Many entity classes are `partial` and the early migrations (2025) are hand-written `CREATE TABLE` SQL. The schema started database-first and was later brought under EF migrations; `20260302191826_DataModelCleanup` added the foreign keys and narrowed `bigint` columns to `integer`.

## Audit stamps

Every domain entity with `created_on`/`updated_on` implements `IAuditable` (`src/FairwayFinder.Shared/IAuditable.cs`), as does `ApplicationUser`. `AuditStampInterceptor` (`src/FairwayFinder.Data/Interceptors/AuditStampInterceptor.cs`) is a `SaveChangesInterceptor` that, on every save:

- `Added`: sets `CreatedOn` and `UpdatedOn` to `DateTime.UtcNow`.
- `Modified`: sets `UpdatedOn`, and marks `CreatedOn` as not modified so an update can never rewrite it.

Rules:

- **Never assign `CreatedOn`/`UpdatedOn` by hand.** The interceptor overwrites them anyway.
- **Do set `CreatedBy`/`UpdatedBy` explicitly.** They are not part of `IAuditable` on purpose. Admin pages act on another user's data and must stamp the acting admin; import and purge jobs have no user. Only the call site knows.
- **`ExecuteUpdate`/`ExecuteDelete` bypass the change tracker and so the interceptor.** Set `UpdatedOn` yourself in an `ExecuteUpdate`.
- The interceptor is a singleton (`AuditStampInterceptor.Instance`) because interceptors are part of the `DbContextOptions` fingerprint; a new instance per context would build a new internal service provider each time.

Covered by `tests/FairwayFinder.Features.Tests/AuditStampTests.cs`.

## Dates and times

| CLR type | Postgres type | Used for |
|---|---|---|
| `DateTime` (always `Kind=Utc`) | `timestamp with time zone` | Audit stamps and event stamps: `created_on`, `updated_on`, `archived_on`, `claimed_on`, `expires_on`, `responded_on`, token/device times, log timestamps |
| `DateOnly` | `date` | A genuine calendar day: `round.date_played`, `game.date_played`, stats filter bounds |

- `date_played` is the golfer's local date, sent by the client, so an evening round is not filed under tomorrow in UTC. Do not convert these to `DateTime`.
- Npgsql rejects a `DateTime` whose `Kind` is `Unspecified` or `Local` for a `timestamptz` parameter. To bridge from a `DateOnly`, use `DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)` (see `AdminDashboardService`). Don't add a value converter: `ToUniversalTime()` on an `Unspecified` value silently produces wrong data instead of failing.
- Until `20260922230819_ConvertAuditStampsToTimestamptz` the audit stamps were `date`. That migration is hand-written because EF's scaffolded `ALTER COLUMN ... TYPE timestamptz` uses Postgres' implicit cast, which reads midnight in the session time zone and shifts every row. It uses `USING col::timestamp AT TIME ZONE 'UTC'` instead, and moves `user_invitation.expires_on` to the end of its day so no outstanding invite was shortened. Any future `date` to `timestamptz` change needs the same treatment. Backfilled rows sit at midnight UTC; real time-of-day precision starts from that migration.

## Soft deletes

Domain tables carry `is_deleted boolean`. Deleting a course, teebox, round, score, game and so on sets `IsDeleted = true` (and `UpdatedBy`) rather than removing the row. There is **no global query filter** (`HasQueryFilter` is not used), so every query must filter `!x.IsDeleted` itself. The filtered unique indexes below include `is_deleted = false` so a soft-deleted row does not block a new live one.

Per the project rule, updates modify existing rows in place rather than soft-deleting and reinserting. `FriendService.SendRequestAsync`, for example, reactivates a `Rejected`/`Cancelled` friendship row instead of inserting a new one.

Tables without soft delete are hard-deleted or never deleted:

| Table | Lifecycle |
|---|---|
| `app_log`, `api_request_log` | Hard-deleted by retention purges (`ExecuteDeleteAsync` in `AppLogService`, `ApiRequestLogService`) |
| `refresh_token` | Revoked by setting `revoked_at` (`JwtTokenService` uses `ExecuteUpdateAsync`) |
| `user_device` | Deactivated with `is_active = false` |
| `golf_course_api_course_map`, `miss_type`, `data_protection_key` | Lookup/framework tables |

Teeboxes also have versioning on top of soft delete: a superseded teebox gets `archived_on`/`archived_by` and stays referenced by historical rounds. See [../features/courses.md](../features/courses.md).

In-progress rounds are a separate concept from deletion: `round.is_complete = false` until posted, and every list/stats/friend query also filters `IsComplete`. See [../features/rounds.md](../features/rounds.md).

## Concurrency: unique indexes as the guard

The codebase does not use row versions, optimistic concurrency tokens or `SELECT ... FOR UPDATE`. Where two devices could race, a **filtered unique index** is the guard and the service catches the violation:

| Index | Race it settles | Handler |
|---|---|---|
| `ix_round_user_id_active` | Two devices start a round at once | `RoundEntryService.StartRoundAsync` returns `ActiveRoundExists` with the winning round's id |
| `ix_score_round_id_hole_id` | Two devices write the same hole for the first time | `RoundEntryService.UpsertHoleAsync` detaches its row, re-reads the winner, applies the score as an update (last write wins) |
| `ix_game_hole_score_participant_hole` | Same, for a guest's hole in a game | `GameService` per-hole upsert, same pattern |
| `ix_game_participant_game_user` | Two devices join the same game | `GameService` returns `AlreadyJoined` |
| `ix_game_join_code_live` | Random join code collides with a live game | `GameService.SaveWithFreshJoinCodeAsync` retries with a new code, up to 5 attempts |
| `ux_friendship_pair_active` | Two users request each other at once | Not caught; the second save fails |

The pattern:

```csharp
try { await dbContext.SaveChangesAsync(); }
catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { /* detach, re-read, resolve */ }
```

`IsUniqueViolation` checks for SQLSTATE `23505` by reflection on the inner exception's type name (`PostgresException`) so `FairwayFinder.Features` does not reference Npgsql directly. It is duplicated privately in `RoundEntryService` and `GameService`.

Multi-step writes in `GameService` open a transaction only when `Database.IsRelational()` so the InMemory unit tests still run. None of this behaviour exists on InMemory, which is why it is tested against real Postgres in the integration tests (see [../testing.md](../testing.md)).

## Adding a migration

There is no `IDesignTimeDbContextFactory`; the EF tools build the Admin host to get the context, so the Admin is always the startup project. `Microsoft.EntityFrameworkCore.Tools` is referenced by `FairwayFinder.Admin`. Install the CLI once with `dotnet tool install --global dotnet-ef` (there is no local tool manifest).

Because the tools run the Admin's `Program.cs` up to `Build()`, the Admin's startup-required configuration must be present (e.g. the `Apns` user-secrets, which `Program.cs` validates before building). `migrations add` does not connect to the database.

**Main database (`ApplicationDbContext`):**

```bash
dotnet ef migrations add <Name> \
  --project src/FairwayFinder.Data \
  --startup-project src/FairwayFinder.Admin \
  --context ApplicationDbContext
```

**Logs database (`LogDbContext`):**

```bash
dotnet ef migrations add <Name> \
  --context LogDbContext \
  --output-dir Migrations/Logs \
  --project src/FairwayFinder.Data \
  --startup-project src/FairwayFinder.Admin
```

`--context` is required whenever the assembly has more than one context; forgetting `--output-dir Migrations/Logs` on the logs context drops the migration into the main folder.

**Applying.** Running the Admin (directly or via the AppHost) applies pending migrations for both contexts. To apply without starting the app, or to produce SQL for review:

```bash
dotnet ef database update --project src/FairwayFinder.Data --startup-project src/FairwayFinder.Admin --context ApplicationDbContext
dotnet ef migrations script --idempotent --project src/FairwayFinder.Data --startup-project src/FairwayFinder.Admin --context ApplicationDbContext
```

**Checklist for a schema change:**

1. Change the entity in `src/FairwayFinder.Data/Entities` and map every new column in `OnModelCreating` with an explicit snake_case `HasColumnName`. Add `IAuditable` and the five audit/soft-delete columns for a new domain entity.
2. Name any index you add with `HasDatabaseName("ix_...")`. If it must exclude deleted rows, add `HasFilter("is_deleted = false")`.
3. Scaffold the migration and read it. Hand-edit when EF's output is wrong for the data: type changes from `date` to `timestamptz`, backfills (`AddIsCompleteToRound` marks existing rounds complete and soft-deletes duplicate scores before creating a unique index), or anything EF cannot express (`ux_friendship_pair_active` uses `LEAST`/`GREATEST` expressions and lives only in raw SQL).
4. Update the `TestData` builders in `tests/FairwayFinder.IntegrationTests.Common` for new required columns.
5. Run `dotnet test`. `MigrationTests` will catch a missing migration.

Constraints created through `migrationBuilder.Sql` are invisible to the EF model and snapshot. `ck_friendship_no_self` and `ux_friendship_pair_active` exist only in `20260426164704_AddFriendshipTable`; keep them in mind if you ever rebuild the `friendship` table.

## MigrationTests

`tests/FairwayFinder.Api.IntegrationTests/Conventions/MigrationTests.cs` runs against the Testcontainers Postgres (`postgres:17-alpine`) shared by the API integration tests. `ApiFactory` applies both contexts' migrations to empty databases, then the tests assert:

| Test | Checks |
|---|---|
| `Every_migration_applies_cleanly_to_an_empty_postgres` | No pending migrations for `ApplicationDbContext` after migrating from scratch |
| `Model_has_no_changes_missing_a_migration` | `HasPendingModelChanges()` is false, i.e. the model matches the snapshot |
| `Every_logs_migration_applies_cleanly_to_an_empty_postgres` | Same for `LogDbContext` |
| `Logs_model_has_no_changes_missing_a_migration` | Same for `LogDbContext` |

The failure message prints the exact `dotnet ef migrations add` command to run. Between tests, `PostgresFixture` wipes both databases with Respawn but keeps `__EFMigrationsHistory`, `miss_type`, `AspNetRoles` and `data_protection_key`, since those rows belong to the schema rather than to a test. Admin startup migration and seeding is covered separately in `FairwayFinder.Admin.IntegrationTests`. More in [../testing.md](../testing.md).
