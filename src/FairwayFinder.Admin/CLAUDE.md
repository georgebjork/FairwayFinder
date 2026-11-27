# FairwayFinder.Admin

UI conventions for the admin console. The root `CLAUDE.md` covers architecture, layering and data rules.

## UI Framework

**100% Radzen components** for all UI. No Bootstrap or other CSS frameworks.

### Render Modes

- **Interactive Server** — default for all pages.
- **Static SSR** — only for the login page (`Components/Auth/`, via `[ExcludeFromInteractiveRouting]`). The error pages use `AuthenticationLayout` and `[AllowAnonymous]` but are not excluded from interactive routing.

### Component Organization

Each domain folder under `Components/Pages/` uses the same `Pages/`, `Components/`, `Dialogs/` subfolder structure;
cross-domain pieces live under `Components/Shared/`.

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

### Displaying scores and games

- Show any hole score with `Shared/Scoring/ScoreMark` (circle/square birdie–bogey notation) and put
  `ScoreMarkLegend` under the card, so every page marks scores the same way. The public profile's
  round pages on the Landing site mirror the same shapes and colours in `site.css` (`.score-mark`);
  change both together.
- The game scorecard (`Games/Components/GameScorecardGrid`) knows nothing about game types. It renders
  `GameCardModel`, which reduces each scoreboard to sides, per-hole winners (`Awards`, with an optional
  tag like `L`/`H`/`×3`), a result chip and a running standing. **A new game type adds one case to
  `GameCardModel.From`** and a test in `GameCardModelTests`; don't add type checks to the grid.
  Without that case the card still shows scores and sides, just no hole winners.
- Adding a whole new game type end to end: use the `new-game-type` skill.

### Style Rules

- ✅ Radzen CSS variables: `style="color: var(--rz-text-color);"`
- ✅ Radzen utility classes: `class="rz-p-4 rz-display-flex"`
- ✅ Radzen component properties: `ButtonStyle="ButtonStyle.Primary"`
- ❌ Hardcoded colors: `style="color: #333;"`
- ❌ Raw CSS when Radzen classes exist: `style="display: flex;"`
