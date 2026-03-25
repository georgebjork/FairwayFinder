# Blazor Page Scaffold

Create a new Blazor page for: $ARGUMENTS

## Page Template

Place the file at: `src/FairwayFinder.Web/Components/Pages/{Domain}/Pages/{PageName}.razor`

```razor
@page "/{route}"
@rendermode InteractiveServer
@attribute [Authorize]
@using System.Security.Claims
@using FairwayFinder.Features.Data
@using FairwayFinder.Features.Services.Interfaces
@using FairwayFinder.Web.Components.Shared.Layout.Breadcrumb
@inject IMyService MyService
@inject AuthenticationStateProvider AuthenticationStateProvider
@inject NavigationManager NavigationManager
@inject NotificationService NotificationService
@inject DialogService DialogService
@inject BreadcrumbState BreadcrumbState

<PageTitle>Page Title</PageTitle>

@if (_loading)
{
    <RadzenStack AlignItems="AlignItems.Center" class="rz-p-8">
        <RadzenProgressBarCircular ShowValue="false" Mode="ProgressBarMode.Indeterminate" />
    </RadzenStack>
}
else
{
    <RadzenStack Gap="1.5rem" class="rz-mx-auto rz-p-4" style="max-width: 1000px;">
        @* Page content using Radzen components *@
    </RadzenStack>
}

@code {
    private bool _loading = true;
    private string _currentUserId = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        BreadcrumbState.Set(
            new BreadcrumbItem("Home", "/"),
            new BreadcrumbItem("Page Title")
        );

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        // Load data from service
        _loading = false;
    }
}
```

## Common Patterns

### Page with route parameter
```razor
@page "/{route}/{Id:long}"

@code {
    [Parameter] public long Id { get; set; }

    // Use OnParametersSetAsync instead of OnInitializedAsync
    protected override async Task OnParametersSetAsync()
    {
        await LoadDataAsync();
    }
}
```

### Cascading organization parameter
```csharp
[CascadingParameter(Name = NamedParameters.OrganizationIdParameter)]
public int OrganizationId { get; set; }
```

### Admin-only page
```razor
@attribute [Authorize(Roles = "Admin")]
```

### Toast notification
```csharp
NotificationService.Notify(new NotificationMessage
{
    Severity = NotificationSeverity.Success, // or Error, Warning, Info
    Summary = "Title",
    Detail = "Description",
    Duration = 4000
});
```

## Rules

- Always `@rendermode InteractiveServer`
- Always `@attribute [Authorize]` (or role-specific)
- Always set breadcrumbs in initialization
- Loading state with centered `RadzenProgressBarCircular`
- Only Radzen components — no Bootstrap, no raw HTML divs for layout
- Use `RadzenStack` for all layout
- Services injected via `@inject`, never DbContext
- Inline `@code` blocks, no code-behind files
- Radzen CSS variables for all colors
- `rz-` utility classes for spacing
