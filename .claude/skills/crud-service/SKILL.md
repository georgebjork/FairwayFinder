---
name: crud-service
description: Generate a complete CRUD service for a new domain entity including Entity, DTOs, Interface, Service, DI registration, and DbSet. Use when creating a new domain feature from scratch.
argument-hint: "[EntityName]"
---

Create a new CRUD service for the domain: $ARGUMENTS

## What to Generate

Generate **all 4 files** for a new domain CRUD feature following the exact patterns in this project.

### 1. Entity — `src/FairwayFinder.Data/Entities/{EntityName}.cs`

Follow the exact entity pattern:

```csharp
namespace FairwayFinder.Data.Entities;

public partial class {EntityName}
{
    public long {EntityName}Id { get; set; }

    // Domain properties...

    public string CreatedBy { get; set; } = null!;
    public DateOnly CreatedOn { get; set; }
    public string UpdatedBy { get; set; } = null!;
    public DateOnly UpdatedOn { get; set; }
    public bool IsDeleted { get; set; }
}
```

### 2. DTOs — `src/FairwayFinder.Features/Data/{EntityName}Dtos.cs`

Include these DTO types:
- **`{EntityName}ListItem`** — Row DTO for data grids (lightweight, only display fields)
- **`{EntityName}DetailResponse`** — Full detail response with all fields and nested objects
- **`Save{EntityName}Request`** — Shared create/update request. Nullable ID (`long?`) means create when null, update when set

```csharp
namespace FairwayFinder.Features.Data;

public class {EntityName}ListItem
{
    public long {EntityName}Id { get; set; }
    // Display fields only
}

public class {EntityName}DetailResponse
{
    public long {EntityName}Id { get; set; }
    // All fields + nested collections
}

public class Save{EntityName}Request
{
    public long? {EntityName}Id { get; set; }
    // Editable fields only
}
```

### 3. Interface — `src/FairwayFinder.Features/Services/Interfaces/I{EntityName}Service.cs`

```csharp
using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Services.Interfaces;

public interface I{EntityName}Service
{
    Task<List<{EntityName}ListItem>> GetAll{EntityName}sAsync();
    Task<{EntityName}DetailResponse?> Get{EntityName}DetailAsync(long id);
    Task<long> Create{EntityName}Async(Save{EntityName}Request request, string userId);
    Task<bool> Update{EntityName}Async(Save{EntityName}Request request, string userId);
    Task<bool> Delete{EntityName}Async(long id, string userId);
}
```

### 4. Service — `src/FairwayFinder.Features/Services/{EntityName}Service.cs`

Follow these exact patterns:
- Inject `IDbContextFactory<ApplicationDbContext>` (NOT DbContext directly)
- Every method: `await using var dbContext = await _dbContextFactory.CreateDbContextAsync();`
- All queries filter `!c.IsDeleted`
- Create: set `CreatedBy`, `CreatedOn`, `UpdatedBy`, `UpdatedOn`, `IsDeleted = false`, return new ID
- Update: check null ID, find entity, update fields + `UpdatedBy`/`UpdatedOn`, return bool
- Delete: soft-delete (`IsDeleted = true`), cascade to children, return bool
- Audit timestamps use `DateOnly.FromDateTime(DateTime.UtcNow)`

```csharp
using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services;

public class {EntityName}Service : I{EntityName}Service
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public {EntityName}Service(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    // Implement all interface methods...
}
```

### 5. Register in DI — `src/FairwayFinder.Features/ServiceRegistration.cs`

Add under the `// Domain services` comment:
```csharp
services.AddTransient<I{EntityName}Service, {EntityName}Service>();
```

### 6. Add DbSet — `src/FairwayFinder.Data/ApplicationDbContext.cs`

Add the DbSet property to ApplicationDbContext:
```csharp
public DbSet<{EntityName}> {EntityName}s { get; set; }
```

## Important Rules

- File-scoped namespaces everywhere
- Nullable reference types enabled
- Return DTOs from service methods, never EF entities
- Services own all business logic — no repository layer
- Use `.Select()` projections in queries, don't load full entities for list operations
- Ask the user what properties/fields the entity needs before generating
