# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build solution
dotnet build FairwayFinder.sln

# Run via Aspire AppHost (recommended - orchestrates PostgreSQL + Web app)
dotnet run --project src/FairwayFinder.AppHost/

# Run tests
dotnet test

# Run single test project
dotnet test tests/FairwayFinder.Features.Tests/
```

## Architecture Overview

Golf stat tracker built with ASP.NET Core 10.0 Blazor Server, Radzen components, and PostgreSQL. The architecture follows a straightforward **Service → EF Core** pattern. Services own all business logic and data access.

### Project Structure

| Project | Role |
|---|---|
| **FairwayFinder.Web** | Blazor Server UI (interactive server mode, static SSR for auth pages) |
| **FairwayFinder.Features** | Services, DTOs, and business logic |
| **FairwayFinder.Data** | EF Core DbContext, entity configurations, migrations |
| **FairwayFinder.Identity** | ASP.NET Core Identity auth configuration |
| **FairwayFinder.Shared** | Shared models and utilities |
| **FairwayFinder.AppHost** | .NET Aspire orchestration for local dev |

### Dependency Flow

```
Web → Features → Data → Shared
Web → Identity → Data
AppHost orchestrates Web + PostgreSQL
```

### Layering Rules

- **Web** calls **Services**. Pages/components never touch DbContext or EF directly.
- **Services** (in Features) contain business logic and query EF Core directly via injected `DbContext`.
- **Data** owns the `DbContext`, entity configurations, and migrations. No repository classes.
- **DTOs** live alongside their services in Features. Services return DTOs to the Web layer, not EF entities.
- **No UI code** in Features or Data (no Radzen references).

## Services (FairwayFinder.Features)

Services are grouped by domain (e.g., `Rounds`, `Players`, `Stats`, `Clubs`, `Courses`).

- Registered with DI, injected into Blazor pages/components.
- Inject `DbContext` directly for all data access — no repository abstraction.
- All methods are async and return DTOs or result objects.
- DTOs live alongside their services, grouped by domain.
- Keep services small and focused — one service per domain area.

## Database

- PostgreSQL in all environments. Dev uses a container via Aspire.
- Schema includes ASP.NET Core Identity tables + golf domain tables.
- EF Core manages all queries and migrations.

## UI Framework

**100% Radzen components** for all UI. No Bootstrap or other CSS frameworks.

### Render Modes

- **Interactive Server** — default for all pages.
- **Static SSR** — only for authentication pages (`Account/Pages/`).

### Component Organization

```
src/FairwayFinder.Web/Components/
├── Pages/          # Routable pages (@page directive)
├── Layout/         # MainLayout, NavMenu (Radzen layout components)
└── Auth/Pages/  # Identity pages (static SSR)
```

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
@attribute [Authorize]
@inject ISomeService SomeService
@inject DialogService DialogService

<PageTitle>Title</PageTitle>

<RadzenCard class="rz-p-5 rz-mx-auto" style="max-width: 1000px;">
    <!-- Radzen components only -->
</RadzenCard>

@code {
    [CascadingParameter(Name = NamedParameters.OrganizationIdParameter)]
    public int OrganizationId { get; set; }

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