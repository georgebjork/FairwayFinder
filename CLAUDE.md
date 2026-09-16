# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build solution
dotnet build FairwayFinder.sln

# Run via Aspire AppHost (recommended - orchestrates PostgreSQL + Admin + Api)
dotnet run --project src/FairwayFinder.AppHost/

# Run tests
dotnet test

# Run single test project
dotnet test tests/FairwayFinder.Features.Tests/
```

## Architecture Overview

Golf stat tracker built on ASP.NET Core 10.0 and PostgreSQL. The architecture follows a straightforward **Service → EF Core** pattern. Services own all business logic and data access.

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
| **FairwayFinder.Identity** | ASP.NET Core Identity user/role types and password policy |
| **FairwayFinder.Agents** | OpenAI agent integration (`ScorecardScoresReaderAgent` for scorecard OCR). **Currently unreferenced** — kept for a future API-side scorecard scan feature |
| **FairwayFinder.Shared** | Shared settings/models |
| **FairwayFinder.ServiceDefaults** | Aspire service defaults (telemetry, health checks) shared by Admin and Api |
| **FairwayFinder.AppHost** | .NET Aspire orchestration for local dev |

### Dependency Flow

```
Admin → Features → Data → Shared
Admin → Identity, ServiceDefaults
Api   → Features → Data → Shared
Api   → Identity, ServiceDefaults
AppHost orchestrates PostgreSQL (+ PgWeb) + Admin + Api
```

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

## UI Framework

**100% Radzen components** for all UI. No Bootstrap or other CSS frameworks.

### Render Modes

- **Interactive Server** — default for all pages.
- **Static SSR** — only for the login page (`Components/Auth/`) and the error pages.

### Component Organization

Each domain folder under `Pages/` uses a consistent subfolder structure:

```
src/FairwayFinder.Admin/Components/
├── Pages/                          # Domain folders
│   ├── Dashboard/Pages/            # "/" overview
│   ├── Users/                      # Pages, Components, Dialogs
│   ├── Rounds/                     # Pages, Components, Dialogs
│   ├── Stats/                      # Pages, Components
│   ├── Courses/                    # Pages, Components, Dialogs
│   ├── Devices/Pages/
│   ├── Invites/                    # Pages, Dialogs
│   ├── SystemOps/                  # Pages, Components (TGTR, imports, connections)
│   └── Diagnostics/Pages/          # Health checks, request logs
├── Shared/                         # Cross-domain shared components
│   ├── Layout/                     # MainLayout (sidebar shell), AuthenticationLayout, Breadcrumb
│   ├── Wrappers/                   # AppHeader, AppFooter
│   └── Dialogs/                    # Shared dialogs (e.g., DeleteConfirmDialog)
├── Auth/Pages/                     # Login (static SSR)
└── Error/                          # Error, NotFound, AccessDenied
```

> Do not name a domain folder `System` — the resulting `...Components.Pages.System` namespace shadows the global `System` namespace for every sibling folder and breaks `using System.*` across the project. That's why it's `SystemOps`.

#### Domain Folder Rules

- **Pages/** — Routable pages with `@page` directive, each carrying `@attribute [Authorize(Policy = Policies.AdminOnly)]`. Every domain must have this.
- **Components/** — Reusable child components (no `@page` directive, no `DialogService.Close()`). Only create when the domain has components.
- **Dialogs/** — Dialog components opened via `DialogService.OpenAsync<T>()`. Only create when the domain has dialogs.
- **Shared/Dialogs/** — Generic dialogs used across multiple domains.
- Only create `Components/` and `Dialogs/` subfolders when there are actual files to put in them.

### When Working from Screenshots

- Use the Radzen MCP server to look up control best practices.
- Match layout using Radzen components and layout controls (e.g., `RadzenStack`).
- Use Radzen CSS classes only when no suitable Radzen layout component exists.

### Radzen CSS Variables (Use These — Never Hardcode Colors)

```css
/* Text */
var(--rz-text-color)
var(--rz-text-secondary-color)
var(--rz-text-disabled-color)

/* Backgrounds */
var(--rz-base-background-color)
var(--rz-panel-background-color)

/* Borders */
var(--rz-border-color)

/* Semantic */
var(--rz-primary)    var(--rz-secondary)
var(--rz-success)    var(--rz-danger)
var(--rz-warning)    var(--rz-info)
```

### Radzen Utility Classes

```html
<!-- Spacing: rz-m-{0-5}, rz-p-{0-5}, rz-mx-auto, rz-my-2, etc. -->
<!-- Flexbox: rz-display-flex, rz-justify-content-between, rz-align-items-center, rz-gap-2 -->
<!-- Text: Use RadzenText with TextStyle.H4, Body1, Body2, etc. -->
```

### Common Components

| Use Case | Component |
|---|---|
| Page containers | `RadzenCard` |
| Buttons | `RadzenButton` (ButtonStyle, Variant, Size) |
| Forms | `RadzenFormField`, `RadzenTextBox`, `RadzenDropDown` |
| Dialogs | `DialogService.OpenAsync<TComponent>()` |
| Notifications | `NotificationService.SendToastMessage()` |
| Data grids | `RadzenDataGrid` |
| Loading | `RadzenProgressBarCircular` |
| Alerts | `RadzenAlert` |

### Style Rules

- ✅ Radzen CSS variables: `style="color: var(--rz-text-color);"`
- ✅ Radzen utility classes: `class="rz-p-4 rz-display-flex"`
- ✅ Radzen component properties: `ButtonStyle="ButtonStyle.Primary"`
- ❌ Hardcoded colors: `style="color: #333;"`
- ❌ Raw CSS when Radzen classes exist: `style="display: flex;"`

## Code Patterns

### Blazor Page

```razor
@page "/path"
@rendermode InteractiveServer
@attribute [Authorize(Policy = Policies.AdminOnly)]
@inject ISomeService SomeService
@inject DialogService DialogService

<PageTitle>Title</PageTitle>

<RadzenCard class="rz-p-5 rz-mx-auto" style="max-width: 1000px;">
    <!-- Radzen components only -->
</RadzenCard>

@code {
    // Call injected services — never DbContext directly from pages
}
```

### Dialog Pattern

```csharp
var result = await DialogService.OpenAsync<MyDialog>("Title",
    new Dictionary<string, object> { { "Param", value } },
    new DialogOptions
    {
        Width = "450px",
        CloseDialogOnOverlayClick = false,
        CloseDialogOnEsc = true
    });

if (result == true)
{
    await LoadData();
    NotificationService.SendToastMessage("Success", NotificationType.Success);
}
```

## Code Conventions

- Nullable reference types enabled.
- File-scoped namespaces: `FairwayFinder.[ProjectName]`.
- EF Core injected directly into services (no repository pattern).
- ASP.NET Core Identity for all auth.
- Inline `@code` blocks in Razor components.