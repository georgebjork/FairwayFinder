# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build solution
dotnet build FairwayFinder.sln

# Run via Aspire AppHost (recommended - orchestrates PostgreSQL + Admin + Api)
dotnet run --project src/FairwayFinder.AppHost/

# Run all tests (unit + integration; integration tests need Docker running)
dotnet test

# Run single test project
dotnet test tests/FairwayFinder.Features.Tests/
dotnet test tests/FairwayFinder.Api.IntegrationTests/
dotnet test tests/FairwayFinder.Admin.IntegrationTests/
```

## Testing

| Project | What it covers |
|---|---|
| **FairwayFinder.Features.Tests** | Unit tests: services built by hand over EF InMemory, scoring engines, calculators. Fast, no Docker. |
| **FairwayFinder.Api.IntegrationTests** | The real API host (`WebApplicationFactory<Program>`) over HTTP: JWT auth, validation, ProblemDetails, every endpoint group. |
| **FairwayFinder.Admin.IntegrationTests** | The real admin host: startup migrations and seeding, page authorization, cookie login, and admin/import services. |
| **FairwayFinder.IntegrationTests.Common** | Shared infrastructure (not a test project): `PostgresFixture`, `FairwayFinderFactory`, `TestData`, fakes. |

Integration tests run against **real Postgres in a Testcontainers container**, one per test assembly,
holding both databases (`db-fairwayfinder` and `db-fairwayfinder-logs`), each wiped with Respawn before each test. They have to use real Postgres: the filtered unique indexes, `ILIKE`,
`ExecuteUpdate`/`ExecuteDelete`, unique-violation retries and migrations all behave differently, or not
at all, on InMemory. Only the edges are faked: `RecordingEmailSender` (read invite/reset links from
`Email`), `FakeApnsClient` (the real `PushNotificationService` still runs), and `StubHttpHandler` for
GolfCourseAPI/TGTR.

**Keep the suite growing with the app:**

- **New API endpoint**: add tests to the matching `Endpoints/*Tests.cs` in Api.IntegrationTests, through HTTP
  with `SignInNewUserAsync()`. `EndpointAuthorizationConventionTests` fails for any endpoint that neither
  requires auth nor is on its explicit anonymous list. Never add to that list without a reason.
- **New admin page**: `PageAuthorizationTests` discovers it by reflection and checks its `AdminOnly` policy and
  redirects automatically. If its route has a new parameter name, add a placeholder in `Resolve`.
  Prerendering is off, so page bodies are not rendered over HTTP. Test the service the page calls instead.
- **New Features service behaviour that depends on Postgres** (constraints, concurrency, raw SQL, `ILike`):
  write an integration test, not an InMemory unit test.
- **New entity or schema change**: `MigrationTests` (in `Api.IntegrationTests/Conventions`) fails if the model changed without a migration.
- **Arranging data**: add builders to `TestData` instead of constructing entities inline, so a schema change is
  fixed in one place. Prefer driving the real flow (e.g. `RoundFlows.PlayCompletedRoundAsync`) over inserting rows.
- **New startup-required config key**: add it to `TestSettings`, or neither host will boot under test.

## Documentation

Developer docs live in `docs/` (index: `docs/README.md`): architecture, schema, every API endpoint, each
feature, the admin console page by page, operations and testing. When you change behaviour a doc
describes, update that doc in the same change.

## Architecture Overview

Golf stat tracker built on ASP.NET Core 11.0 and PostgreSQL. The architecture follows a straightforward **Service → EF Core** pattern. Services own all business logic and data access.

There are two hosts and they have very different jobs:

- **FairwayFinder.Api** is the only end-user surface. The iOS app talks to it over JWT-authenticated REST. It also serves the small public web surface that remains: the Apple app-site-association file and landing pages for invite and password-reset deep links.
- **FairwayFinder.Admin** is an internal Blazor Server console. Every page requires the `Admin` role. It is for the things a phone should not do — user management, invites, course and teebox data entry, cross-user round inspection and repair, stats/strokes-gained diagnostics, request logs, health, and the TGTR / GolfCourseAPI import tools. It also owns EF migrations and role seeding at startup.

There is no public-facing web UI. Don't add end-user features to the admin console — they belong in the API.

### Project Structure

| Project | Role |
|---|---|
| **FairwayFinder.Admin** | Admin-only Blazor Server console (interactive server mode, static SSR for login) |
| **FairwayFinder.Api** | JWT-secured REST API (Minimal APIs) consumed by the iOS app — the only end-user surface |
| **FairwayFinder.Features** | Services, DTOs, and business logic shared by Admin and Api |
| **FairwayFinder.Data** | EF Core DbContext, entity configurations, migrations |
| **FairwayFinder.Identity** | ASP.NET Core Identity user/role types. The password policy is configured in each host (`Api/Program.cs`, `Admin/Startup/AuthenticationConfiguration.cs`); keep the two identical |
| **FairwayFinder.Agents** | LLM agent integration with OpenAI and Claude factories (`ScorecardScoresReaderAgent` for scorecard OCR). **Currently unreferenced** — kept for a future API-side scorecard scan feature |
| **FairwayFinder.Landing** | Static marketing site for fairwayfinder.pro: Razor components rendered to HTML at build time, hosted as a DigitalOcean App Platform static site. References no other project |
| **FairwayFinder.Shared** | Shared settings/models |
| **FairwayFinder.ServiceDefaults** | Aspire service defaults (telemetry, health checks) shared by Admin and Api |
| **FairwayFinder.AppHost** | .NET Aspire orchestration for local dev |

### Host registration and auth

Both hosts call `RegisterFeatureServices()` for the shared domain layer. The Admin app additionally calls `RegisterAdminServices()`, which registers the cross-user admin services, the TGTR transfer tool, and the GolfCourseAPI import pipeline (including its hosted background job) — none of which the API can reach, so it doesn't register them.

Authentication differs by host: Admin uses Identity cookies with an `AdminOnly` policy on every page; Api uses JWT bearer tokens.

### Layering Rules

- **Admin** and **Api** call **Services**. Pages/components/endpoints never touch DbContext or EF directly.
- **Services** (in Features) contain business logic and query EF Core directly via injected `DbContext`.
- **Data** owns the `DbContext`, entity configurations, and migrations. No repository classes.
- **DTOs** live alongside their services in Features. Services return DTOs to the Admin/Api layers, not EF entities.
- **No UI code** in Features or Data (no Radzen references).
- **Admin/Services** is for UI-layer infrastructure only (`CircuitTrackingService`, `ApplicationStartupService` for migrations + role seeding). Domain services belong in Features.
- **Acting on another user's data**: admin pages must keep the acting admin and the target user separate. Pass the target user id in as a parameter; use the admin's id only for audit stamping. `AdminRoundService.UpdateRoundAsAdminAsync` is the model — it re-resolves the round's owner server-side rather than trusting a caller-supplied `UserId`.

## Services (FairwayFinder.Features)

Services are grouped by domain (e.g., `Rounds`, `Players`, `Stats`, `Clubs`, `Courses`).

- Registered with DI, injected into Blazor pages/components.
- Inject `DbContext` directly for all data access — no repository abstraction.
- All methods are async and return DTOs or result objects.
- DTOs live alongside their services, grouped by domain.
- Keep services small and focused — one service per domain area.

### Rounds: two services, one set of scoring rules

Rounds are the exception to one-service-per-domain, split by lifecycle rather than by domain:

- **`RoundService`** — reading rounds, and the deprecated atomic whole-round submit (`POST /api/rounds`).
- **`RoundEntryService`** — hole-by-hole entry: `POST /rounds/start`, `PUT /rounds/{id}/holes/{n}`,
  `DELETE /rounds/{id}/holes/{n}`, `POST /rounds/{id}/complete`, `GET /rounds/active`. This is how
  the iOS app logs a round, writing each hole as it's played rather than everything at the end.

The scoring arithmetic both paths need lives in `Helpers/RoundScoringHelper` (totals, scoring
distribution, shape flags, shot numbering, hole-stat derivation) so the two cannot drift. Put new
scoring rules there, not in either service.

**`round.is_complete`** is false while a round is being entered. Every list, stats, and friend
query filters `IsComplete == true` — an in-progress round's running score would otherwise read as
a great round. `GetRoundByIdAsync` is deliberately *not* gated: it's what the resume endpoint and
the admin console use. `ix_round_user_id_active` enforces one open round per golfer in the
database, and `ix_score_round_id_hole_id` makes the per-hole upsert safe under concurrency.

Features cannot throw the API's exception types (the dependency runs Api → Features), so
`RoundEntryService` returns `RoundEntryResult<T>` and `RoundEndpoints` maps the status to HTTP.

## Database

- PostgreSQL in all environments. Dev uses a container via Aspire.
- Schema includes ASP.NET Core Identity tables + golf domain tables.
- EF Core manages all queries and migrations.

### Two databases

| Connection string | Context | Holds | Migrations |
|---|---|---|---|
| `fairwayfinder` | `ApplicationDbContext` | Identity, golf data, Data Protection keys | `Migrations/` |
| `fairwayfinderlogs` | `LogDbContext` | `api_request_log`, `app_log` | `Migrations/Logs` (`--context LogDbContext --output-dir Migrations/Logs`) |

Same server in production, different database, so log volume and retention purges never touch golf
data. Admin migrates both at startup. Diagnostics go in `LogDbContext`, never in `ApplicationDbContext`.

### Logging

Serilog owns logging in both hosts (`ServiceDefaults/LoggingExtensions.cs`): readable console everywhere
(no timestamp outside Development, since the host prefixes one), OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Levels live in the
`Serilog` config section, not `Logging`.

- **Warning and worse also go to `app_log`** through `DatabaseLogSink` (Features/Services/Logging), a DI
  `ILogEventSink` that `ReadFrom.Services` picks up. The admin console shows them at `/app-logs`.
  Settings: the `AppLogging` section.
- **Who caused it**: `UseUserLogContext()` pushes `UserId`/`UserEmail` onto every event in an authenticated
  request; `LogContextCircuitHandler` does the same inside Admin's Blazor circuits.
- **Log with message templates, never interpolation** (`LogError(ex, "Round {RoundId} failed", id)`): the
  template groups occurrences and each value is stored as a property.
- **Pick levels deliberately**: everything at Warning+ is persisted. A 4xx is the client's mistake, not
  an error — `GlobalExceptionHandler` logs those at Information.
- `LogDbContext` uses `NullLoggerFactory` and the writer runs inside `LogWriteScope`, so a failing log
  write cannot log about itself back into the sink. Keep both.

### Dates and audit stamps

Every entity carrying `created_on`/`updated_on` implements `IAuditable` (`FairwayFinder.Shared`), and
`AuditStampInterceptor` (registered from `ApplicationDbContext.OnConfiguring`) stamps both with
`DateTime.UtcNow` on save. **Never assign them by hand** — only `CreatedBy`/`UpdatedBy`, which stay
explicit because admin pages act on another user's data and must record the acting admin, and the
import/purge jobs have no user at all.

- Audit and event stamps are `DateTime` on `timestamp with time zone`, always holding UTC.
- `DateOnly`/`date` is reserved for a genuine calendar date — `Round.DatePlayed`, `Game.DatePlayed`,
  and the stats filter bounds. A round is played on a day, and the client sends its own local date so
  an evening round isn't filed under tomorrow (`RoundEntryDtos.cs`). Don't "fix" these to `DateTime`.
- Npgsql rejects a `DateTime` whose `Kind` is `Unspecified` or `Local` on a `timestamptz` column, so
  bridge from a `DateOnly` with `DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)`
  — see `AdminDashboardService`. Don't paper over this with a value converter: `ToUniversalTime()` on
  an `Unspecified` value turns a loud crash into silently wrong data.
- `ExecuteUpdate`/`ExecuteDelete` bypass the change tracker and so the interceptor.
- Changing a `date` column to `timestamptz` in a migration needs an explicit
  `USING x::timestamp AT TIME ZONE 'UTC'`. EF scaffolds a bare `ALTER COLUMN ... TYPE`, which
  succeeds via Postgres' implicit cast but resolves midnight in the *session* time zone — silently
  shifting every row. See `20260922230819_ConvertAuditStampsToTimestamptz`.

## Admin UI

Admin console UI conventions (100% Radzen, render modes, domain folder rules, score and game display, style
rules) live in `src/FairwayFinder.Admin/CLAUDE.md`, which loads when working in that project.

- Hole-score marks (`Shared/Scoring/ScoreMark` in Admin) are mirrored on the Landing site's public round pages
  in `site.css` (`.score-mark`); change both together.
- Adding a whole new game type end to end: use the `new-game-type` skill.

## Code Conventions

- Nullable reference types enabled.
- File-scoped namespaces: `FairwayFinder.[ProjectName]`.
- EF Core injected directly into services (no repository pattern).
- ASP.NET Core Identity for all auth.
- Inline `@code` blocks in Razor components.