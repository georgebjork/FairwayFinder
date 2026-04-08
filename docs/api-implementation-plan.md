# FairwayFinder REST API Implementation Plan

## Context

The golf stat tracker currently only has a Blazor Server web app. A mobile front-end is needed, which requires a REST API. The `FairwayFinder.Api` project already exists as a skeleton (template weatherforecast endpoint) with references to Data, Features, Identity, and Shared. The goal is to wire up JWT authentication against existing Identity users and expose the existing domain services (rounds, courses, stats) via minimal API endpoints.

## File Structure

All new files under `src/FairwayFinder.Api/`:

```
Auth/
  JwtSettings.cs              -- Settings POCO
  JwtTokenService.cs          -- Token generation + refresh
  AuthEndpoints.cs            -- POST /api/auth/login, /api/auth/refresh
  AuthDtos.cs                 -- LoginRequest, LoginResponse, RefreshRequest
Endpoints/
  CourseEndpoints.cs           -- /api/courses/*
  RoundEndpoints.cs            -- /api/rounds/*
  StatsEndpoints.cs            -- /api/stats/*
  LookupEndpoints.cs           -- /api/lookups/*
  ProfileEndpoints.cs          -- /api/profile
Exceptions/
  HttpResponseException.cs     -- Base exception with StatusCode
  NotFoundException.cs         -- 404 for missing resources (rounds, courses, etc.)
  ForbiddenException.cs        -- 403 for ownership violations
  GlobalExceptionHandler.cs    -- IExceptionHandler that maps exceptions to ProblemDetails
Extensions/
  ClaimsPrincipalExtensions.cs -- GetUserId() from JWT claims
Validators/
  LoginRequestValidator.cs     -- FluentValidation for login
  CreateRoundRequestValidator.cs -- FluentValidation for round creation
  UpdateRoundRequestValidator.cs -- FluentValidation for round updates
```

Modified files:
- `Program.cs` -- rewrite with DI, auth, middleware, endpoint mapping
- `FairwayFinder.Api.csproj` -- add NuGet packages
- `appsettings.json` -- add connection string, JWT config, required service config sections
- `src/FairwayFinder.AppHost/AppHost.cs` -- wire up the API project with DB reference

## Step 1: Project Setup

### 1a. NuGet Packages (`FairwayFinder.Api.csproj`)
- `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.0)
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (10.0.0)
- `Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.0)
- `FluentValidation.DependencyInjectionExtensions` -- auto-registers all validators from assembly

### 1b. `appsettings.json`
Add connection string, JWT section, and Tgtr/GolfCourseApi sections (required by `RegisterFeatureServices` or it throws):
```json
{
  "ConnectionStrings": { "fairwayfinder": "..." },
  "Jwt": { "Secret": "...", "Issuer": "FairwayFinder.Api", "Audience": "FairwayFinder.Mobile", "AccessTokenExpirationMinutes": 60, "RefreshTokenExpirationDays": 30 },
  "Tgtr": { "BaseUrl": "https://tgtr-api.azurewebsites.net" },
  "GolfCourseApi": { "BaseUrl": "https://api.golfcourseapi.com", "ApiKey": "" }
}
```

### 1c. AppHost (`src/FairwayFinder.AppHost/AppHost.cs`)
Add the API project alongside the Web project:
```csharp
builder.AddProject<Projects.FairwayFinder_Api>("fairwayfinder-api")
    .WithReference(dbFairwayfinder)
    .WaitFor(dbFairwayfinder);
```

## Step 2: Exception Handling & Validation

### 2a. `Exceptions/HttpResponseException.cs`
Base exception class that carries a status code and optional value:
```csharp
public class HttpResponseException(int statusCode, string message, object? value = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public object? Value { get; } = value;
}
```

### 2b. Domain Exceptions
Specialized exceptions that throw from endpoints (not services). These keep endpoint code clean -- throw instead of returning `Results.NotFound()` manually.

- **`NotFoundException`** -- extends `HttpResponseException` with 404. Constructor takes entity name + ID (e.g., `new NotFoundException("Round", roundId)`).
- **`ForbiddenException`** -- extends `HttpResponseException` with 403. For ownership violations (e.g., user tries to access another user's round).

### 2c. `Exceptions/GlobalExceptionHandler.cs`
Implements `IExceptionHandler`. Catches all unhandled exceptions and maps them to ProblemDetails responses:

```csharp
exception switch
{
    HttpResponseException httpEx => Results.Problem(detail: httpEx.Message, statusCode: httpEx.StatusCode),
    ValidationException valEx   => Results.ValidationProblem(errors: grouped validation errors),
    _                           => Results.Problem(detail: "An unexpected error occurred", statusCode: 500)
}
```

Logs every exception before returning. Registered via `builder.Services.AddExceptionHandler<GlobalExceptionHandler>()` and `app.UseExceptionHandler()`.

### 2d. FluentValidation Setup
- Register all validators: `builder.Services.AddValidatorsFromAssemblyContaining<Program>()`
- Create a **validation endpoint filter** that auto-validates request bodies before the handler runs. Apply it to endpoint groups via `.AddEndpointFilter<ValidationFilter<T>>()`.
- Validators throw `ValidationException` on failure, which `GlobalExceptionHandler` catches and formats as `Results.ValidationProblem()`.

### 2e. Validators (`Validators/`)
- **`LoginRequestValidator`** -- Email required + valid format, Password required + min 8 chars.
- **`CreateRoundRequestValidator`** -- CourseId > 0, TeeboxId > 0, DatePlayed required, Holes not empty. Nested rules for HoleScoreEntry (Score >= 0, Par > 0, etc.).
- **`UpdateRoundRequestValidator`** -- Same as create + RoundId > 0.

## Step 3: Auth Infrastructure

### 3a. `Auth/JwtSettings.cs`
Settings POCO: Secret, Issuer, Audience, AccessTokenExpirationMinutes (default 60), RefreshTokenExpirationDays (default 30).

### 3b. `Auth/AuthDtos.cs`
- `LoginRequest(string Email, string Password)`
- `LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, string UserId, string Email, string? FirstName, string? LastName)`
- `RefreshRequest(string RefreshToken)`

### 3c. `Auth/JwtTokenService.cs` (registered as singleton)
- `GenerateAccessToken(ApplicationUser user, IList<string> roles)` -- creates JWT with claims: sub=UserId, email, given_name, family_name, role claims. Signs with SymmetricSecurityKey from JwtSettings.Secret.
- `GenerateRefreshToken()` -- cryptographically random string, stored in an in-memory `ConcurrentDictionary<string, (string UserId, DateTime Expiry)>`.
- `ValidateRefreshToken(string refreshToken)` -- returns userId if valid and not expired, null otherwise. Removes used token (one-time use).

### 3d. `Auth/AuthEndpoints.cs`
Route group: `/api/auth` (anonymous)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/login` | Validate via `SignInManager.CheckPasswordSignInAsync` (not `PasswordSignInAsync` -- no cookie). Issue JWT + refresh token. |
| POST | `/api/auth/refresh` | Validate refresh token, look up user, issue new pair. |

### 3e. `Extensions/ClaimsPrincipalExtensions.cs`
`GetUserId()` -- extracts `ClaimTypes.NameIdentifier` from JWT claims. Used by all protected endpoints.

## Step 4: Domain Endpoints

All route groups require authorization. Each is a static class with `Map{X}Endpoints(this WebApplication app)`.

### 4a. Course Endpoints (`/api/courses`)
Reuses: `ICourseService` from Features

| Method | Route | Service Call | Returns |
|--------|-------|-------------|---------|
| GET | `/search?query={q}` | `SearchCoursesAsync(query)` | `List<CourseSearchResult>` |
| GET | `/{courseId}/teeboxes` | `GetTeeboxesAsync(courseId)` | `List<TeeboxOption>` |
| GET | `/teeboxes/{teeboxId}/holes` | `GetHolesAsync(teeboxId)` | `List<HoleInfo>` |

### 4b. Round Endpoints (`/api/rounds`)
Reuses: `IRoundService` from Features

| Method | Route | Service Call | Notes |
|--------|-------|-------------|-------|
| GET | `/` | `GetRoundsByUserIdAsync(userId)` | List user's rounds |
| GET | `/details` | `GetRoundsWithDetailsAsync(userId)` | Full details with holes/stats |
| GET | `/{roundId}` | `GetRoundByIdAsync(roundId)` | Throws `NotFoundException` / `ForbiddenException` |
| POST | `/` | `CreateRoundAsync(request)` | **Set request.UserId from JWT**, validated via `CreateRoundRequestValidator`, return Created |
| PUT | `/{roundId}` | `UpdateRoundAsync(request)` | Validated via `UpdateRoundRequestValidator`, throws `NotFoundException` / `ForbiddenException` |
| DELETE | `/{roundId}` | `DeleteRoundAsync(roundId, userId)` | Throws `NotFoundException` / `ForbiddenException` |
| GET | `/{roundId}/shots` | `GetShotsByRoundIdAsync(roundId)` | Throws `NotFoundException` / `ForbiddenException` |
| GET | `/courses` | `GetPlayedCoursesByUserId(userId)` | Courses user has played |

**Critical:** Always overwrite `request.UserId` from JWT claims, never trust client-supplied value.

### 4c. Stats Endpoints (`/api/stats`)
Reuses: `IStatsService` from Features

| Method | Route | Service Call |
|--------|-------|-------------|
| GET | `/` | `GetUserStatsAsync(userId, filter?)` with optional query params |
| GET | `/courses/{courseId}` | `GetCourseStatsAsync(userId, courseId, teeboxId?, startDate?, endDate?)` |
| GET | `/years` | `GetAvailableYearsAsync(userId)` |
| GET | `/courses` | `GetUserCoursesAsync(userId)` |

### 4d. Lookup Endpoints (`/api/lookups`)
| Method | Route | Source |
|--------|-------|--------|
| GET | `/miss-types` | Query `ApplicationDbContext.MissTypes` via `IDbContextFactory` |
| GET | `/lie-types` | Return `LieType` enum values as `{ value, name }` list |
| GET | `/distance-units` | Return `DistanceUnit` enum values |

### 4e. Profile Endpoints (`/api/profile`)
Reuses: `IProfileService` from Features

| Method | Route | Service Call |
|--------|-------|-------------|
| GET | `/` | `GetOrCreateProfileAsync(userId)` |

## Step 5: Program.cs

Pipeline order:
1. DbContextFactory + Npgsql
2. AddIdentity (same password policy as Web, NO cookie config, NO `.AddSignInManager()`)
3. AddAuthentication (default scheme = JwtBearer) + AddJwtBearer with validation params
4. AddAuthorization
5. `RegisterFeatureServices()` (reuse existing registration)
6. JwtTokenService (singleton)
7. `AddExceptionHandler<GlobalExceptionHandler>()` + `AddProblemDetails()`
8. `AddValidatorsFromAssemblyContaining<Program>()`
9. AddOpenApi
10. Build app
11. Middleware: `UseExceptionHandler()`, OpenApi (dev only), UseHttpsRedirection, UseAuthentication, UseAuthorization
12. Map all endpoint groups
13. app.Run()

**Note:** `UseExceptionHandler()` must be early in the pipeline so it catches exceptions from all downstream middleware and endpoints.

## Step 6: Things to Watch

1. **`RegisterFeatureServices` requires config sections** for Tgtr and GolfCourseApi or it throws at startup. Include them in appsettings.json.
2. **`GolfCourseApiImportJob` hosted service** will also start in the API process. It's harmless (does nothing unless triggered) but worth noting.
3. **No CORS needed** for native mobile clients. Add later if testing from browser.
4. **`DateOnly` serialization** works natively in .NET 10. Mobile client sends `"2024-06-15"` format.
5. **No new migrations or DB changes.** The API uses the same schema via the same `ApplicationDbContext`.
6. **No changes to Features, Data, Identity, or Shared projects.** The API is purely additive.

## Verification

1. `dotnet build FairwayFinder.sln` -- all projects compile
2. `dotnet run --project src/FairwayFinder.AppHost/` -- API starts, connects to Aspire-managed PostgreSQL
3. Test `POST /api/auth/login` with seeded user credentials -- get JWT back
4. Test `GET /api/courses/search?query=test` with `Authorization: Bearer {jwt}` -- verify protected endpoints work
5. Test `POST /api/rounds` -- verify round creation works end-to-end

## Key Files to Reference During Implementation

- `src/FairwayFinder.Web/Startup/AuthenticationConfiguration.cs` -- Identity config to mirror (minus cookies)
- `src/FairwayFinder.Features/ServiceRegistration.cs` -- reuse `RegisterFeatureServices`
- `src/FairwayFinder.Features/Services/Interfaces/IRoundService.cs` -- round endpoint signatures
- `src/FairwayFinder.Features/Services/Interfaces/ICourseService.cs` -- course endpoint signatures
- `src/FairwayFinder.Features/Services/Interfaces/IStatsService.cs` -- stats endpoint signatures
- `src/FairwayFinder.Features/Data/CreateRoundRequest.cs` -- round creation DTO
- `src/FairwayFinder.Data/ApplicationDbContext.cs` -- for lookup queries
