# Getting started

How to get FairwayFinder building, running and tested on a developer machine. The short version: install the .NET 11 SDK and Docker, set a handful of secrets, and run the Aspire AppHost.

## Prerequisites

| Tool | Why |
|---|---|
| .NET 11 SDK | Every project targets `net11.0`. CI uses `11.0.x`. |
| Docker (Desktop, OrbStack, Colima...) | Aspire runs Postgres in a container, and the integration tests start their own Postgres with Testcontainers. |
| An IDE | Rider or Visual Studio both work. The repo carries a Rider `.DotSettings.user` and `.idea` folder. |

You do not need the `aspire` CLI. `dotnet run` on the AppHost project is enough, because `src/FairwayFinder.AppHost/FairwayFinder.AppHost.csproj` uses the `Aspire.AppHost.Sdk`.

## Build

```bash
dotnet build FairwayFinder.sln
```

## Run everything with Aspire (recommended)

```bash
dotnet run --project src/FairwayFinder.AppHost/
```

`src/FairwayFinder.AppHost/AppHost.cs` starts:

- **`postgres`**: a persistent Postgres container named `fairwayfinder-postgres` on port 5432, image `17.6-bookworm`, data in the Docker volume `fairwayfinder-data`. It holds two databases, `db-fairwayfinder` (connection name `fairwayfinder`) and `db-fairwayfinder-logs` (`fairwayfinderlogs`). The container outlives the AppHost (`ContainerLifetime.Persistent`), so your data survives restarts.
- **PgWeb** (`fairwayfinder-pgweb`), a browser SQL client. It has explicit start, so click Start on it in the dashboard when you want it.
- **`fairwayfinder-admin`**: the admin console. It waits for both databases, then runs migrations and seeding on startup.
- **`fairwayfinder-api`**: the API, with a dashboard link to the Scalar API reference at `/scalar/`.
- **`fairwayfinder-landing`**: the static marketing site, generated at startup and served on `http://localhost:5310`. It doesn't watch files, so restart it in the dashboard after editing a page.

The Aspire dashboard opens at `https://localhost:17065` (https launch profile) or `http://localhost:15238` (http). Each project's main link is labelled "Admin console" / "API" / "Landing page".

Do not bump the Postgres image tag casually. The comment in `AppHost.cs` explains why it is pinned: Aspire 13.6 defaults to Postgres 18, which cannot open the existing PG17 volume, and the plain `17.6` tag moved from Debian bookworm to trixie, whose glibc collation version breaks `CREATE DATABASE`. Either change needs a dump/restore or a reindex first.

### Secrets the AppHost asks for

The AppHost declares parameters. Missing values do not crash startup: the dashboard prompts for them. To avoid the prompt, store them in the AppHost's user secrets:

```bash
cd src/FairwayFinder.AppHost
dotnet user-secrets set "Parameters:jwt-secret" "<at least 32 characters>"
dotnet user-secrets set "Parameters:apns-bundle-id" "<bundle id>"
dotnet user-secrets set "Parameters:apns-key-id" "<key id>"
dotnet user-secrets set "Parameters:apns-team-id" "<team id>"
dotnet user-secrets set "Parameters:apns-p8-contents" "<contents of the .p8 key>"
dotnet user-secrets set "Parameters:resend-api-key" "<anything; dev uses DevEmailSender>"
dotnet user-secrets set "Parameters:golfcourseapi-api-key" "<GolfCourseAPI key>"
```

`postgres-username` and `postgres-password` default to `user` / `password` in `src/FairwayFinder.AppHost/appsettings.Development.json`.

| Parameter | Passed to | As |
|---|---|---|
| `jwt-secret` | Api | `Jwt__Secret` |
| `apns-bundle-id`, `apns-key-id`, `apns-team-id`, `apns-p8-contents` | Api and Admin | `Apns__BundleId`, `Apns__KeyId`, `Apns__TeamId`, `Apns__P8Contents` |
| `resend-api-key` | Admin | `Resend__ApiKey` |
| `golfcourseapi-api-key` | Admin | `GolfCourseApi__ApiKey` |

Both hosts refuse to start if any of the four APNs values is empty, even though the admin console never sends a push (see [Hosting and configuration](architecture/hosting-and-configuration.md#startup-checks)). For local work that doesn't touch push, any non-empty placeholder strings will let them boot; the client is only built on first use. In Development both hosts use the APNs sandbox and `DevEmailSender`, which logs emails instead of sending them.

### Signing in to the admin console

Every admin page requires the `Admin` role. On a fresh database, give the admin host a seed user through its user secrets and restart it:

```bash
cd src/FairwayFinder.Admin
dotnet user-secrets set "SeedUser:Email" "you@example.com"
dotnet user-secrets set "SeedUser:Password" "<8+ characters>"
dotnet user-secrets set "SeedUser:FirstName" "First"
dotnet user-secrets set "SeedUser:LastName" "Last"
```

`ApplicationStartupService.SeedDefaultUserAsync` (`src/FairwayFinder.Admin/Services/ApplicationStartupService.cs`) creates the user once, confirmed, in the `Admin` role, with a profile. If the email already exists it does nothing.

### Calling the API locally

In Development the API maps the OpenAPI document (`/openapi/v1.json`) and Scalar (`/scalar/`). Register or log in through `/api/auth/...` to get a JWT, then use Scalar's bearer auth. (`src/FairwayFinder.Api/FairwayFinder.Api.http` is still the project template's `/weatherforecast` request and is not useful.) See [API overview](api/overview.md).

## Running a host without Aspire

Each host can run on its own (`dotnet run --project src/FairwayFinder.Api/`), but then nothing injects the connection strings or secrets. The fallback connection strings in each `appsettings.json` point at `Host=localhost;Database=db-fairwayfinder` and `db-fairwayfinder-logs` with `user`/`password`, which matches the Aspire container, so starting the AppHost once (to create the container) and then running a host directly works. Set the secrets in that host's own user secrets (`Jwt:Secret`, `Apns:*`, and for Admin `GolfCourseApi:ApiKey`).

Run the admin console at least once before the API on an empty database: only Admin applies migrations.

| Host | http | https |
|---|---|---|
| Api | `http://localhost:5227` | `https://localhost:7077` |
| Admin | `http://localhost:5142` | `https://localhost:7188` |

## Tests

```bash
dotnet test                                          # everything
dotnet test tests/FairwayFinder.Features.Tests/      # unit tests, no Docker
dotnet test tests/FairwayFinder.Api.IntegrationTests/
dotnet test tests/FairwayFinder.Admin.IntegrationTests/
```

Integration tests need Docker running. They start their own Postgres container per test assembly and do not touch your dev database. See [Testing](testing.md).

## Migrations

EF migrations live in `src/FairwayFinder.Data/Migrations` (golf data) and `src/FairwayFinder.Data/Migrations/Logs` (logs). The admin host applies both on startup. How to add one is in [Database](data/database.md).

## The `scripts/` folder

Ad-hoc database chores, run by hand. Nothing in the build uses them.

| File | What it does |
|---|---|
| `scripts/get-db-data-from-dev.md` | Notes for copying a remote database locally: `pg_dump` inside the container, `psql` restore into `db-fairwayfinder`, and a PL/pgSQL block that rewrites every `user_id` column from one user id to another (useful after restoring someone else's data and wanting it under your local account). |
| `scripts/cleanup-course-data.sql` | Hard-deletes courses with no city/state/country, a name with no letters, or no live teeboxes, along with their holes, teeboxes and `golf_course_api_course_map` rows. Ends with a SELECT to preview what would match. Mostly cleanup after GolfCourseAPI imports ([Course imports](features/course-imports.md)). It does not touch rounds; rounds reference courses and teeboxes with restrict foreign keys, so if any matching course has been played the whole statement fails. Run the preview first. |

## Developer tooling in the repo

- **`CLAUDE.md`** is the project's working guide for AI coding agents and doubles as a concise architecture summary.
- **`.claude/skills/`** holds Claude Code skills that scaffold code in the house style: `crud-service` (entity, DTOs, service, DI registration), `list-page`, `detail-page`, `blazor-page`, `form-dialog` (admin Radzen pages and dialogs), `new-game-type` (a new side game end to end; see [Games](features/games.md)), and `humanizer` (prose cleanup).
- **`.mcp.json`** and **`opencode.json`** register the Radzen MCP server (`radzen.mcp`, configured under the name `radezen-mcp`) so agents can look up Radzen component usage. Install the `radzen.mcp` tool yourself if you want it.
- **`.claude/settings.json`** enables the C# LSP and frontend-design Claude Code plugins.

## Next

Read the [Architecture overview](architecture/overview.md), then [Hosting and configuration](architecture/hosting-and-configuration.md).
