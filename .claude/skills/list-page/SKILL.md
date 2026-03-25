---
name: list-page
description: Generate a data grid list page with search, pagination, and CRUD actions using RadzenDataGrid. Use when creating a page that shows a list of entities.
argument-hint: "[EntityName]"
---

Create a new data grid list page for: $ARGUMENTS

## What to Generate

### List Page — `src/FairwayFinder.Web/Components/Pages/{Domain}/Pages/{EntityName}List.razor`

Follow this exact structure:

```razor
@page "/{route}"
@rendermode InteractiveServer
@attribute [Authorize]
@using System.Security.Claims
@using FairwayFinder.Features.Data
@using FairwayFinder.Features.Services.Interfaces
@using FairwayFinder.Web.Components.Pages.{Domain}.Dialogs
@using FairwayFinder.Web.Components.Shared.Layout.Breadcrumb
@inject I{EntityName}Service {EntityName}Service
@inject AuthenticationStateProvider AuthenticationStateProvider
@inject NavigationManager NavigationManager
@inject NotificationService NotificationService
@inject DialogService DialogService
@inject BreadcrumbState BreadcrumbState
@implements IDisposable

<PageTitle>{Page Title}</PageTitle>

<RadzenStack Gap="1.5rem" class="rz-mx-auto rz-p-4">
    @* Header with action button *@
    <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" JustifyContent="JustifyContent.End">
        <RadzenButton Text="Add {EntityName}" Icon="add" ButtonStyle="ButtonStyle.Primary" Size="ButtonSize.Small"
                      Click="OnAddClicked" />
    </RadzenStack>

    @* Loading state *@
    @if (_loading)
    {
        <RadzenStack AlignItems="AlignItems.Center" class="rz-p-8">
            <RadzenProgressBarCircular ShowValue="false" Mode="ProgressBarMode.Indeterminate" />
        </RadzenStack>
    }
    @* Empty state *@
    else if (_items.Count == 0)
    {
        <RadzenAlert AlertStyle="AlertStyle.Info" Shade="Shade.Lighter" AllowClose="false">
            No items found. Click "Add {EntityName}" to create one.
        </RadzenAlert>
    }
    @* Data grid *@
    else
    {
        <RadzenCard>
            <RadzenStack Gap="1rem">
                @* Search box with debounce *@
                <RadzenTextBox Placeholder="Search..." Value="@_searchText"
                               @oninput="@OnSearchInput" Style="width: 100%;" />

                <RadzenDataGrid Data="@FilteredItems"
                                TItem="{EntityName}ListItem"
                                AllowSorting="true"
                                AllowPaging="true"
                                PageSize="10"
                                PageSizeOptions="@(new[] { 10, 25, 50, 100 })"
                                PagerHorizontalAlign="HorizontalAlign.Right"
                                ShowPagingSummary="true"
                                PageSizeText="per page"
                                Style="cursor: pointer;"
                                @bind-Value="@_selected"
                                RowClick="@OnRowClick">
                    <Columns>
                        @* Define columns with Property, Title, Width *@
                        <RadzenDataGridColumn TItem="{EntityName}ListItem" Property="Name" Title="Name" />
                    </Columns>
                </RadzenDataGrid>
            </RadzenStack>
        </RadzenCard>
    }
</RadzenStack>

@code {
    private List<{EntityName}ListItem> _items = new();
    private bool _loading = true;
    private string _currentUserId = string.Empty;
    private string _searchText = string.Empty;
    private string _pendingSearchText = string.Empty;
    private Timer? _debounceTimer;
    private IList<{EntityName}ListItem> _selected = [];

    private const int DebounceMs = 300;

    private IEnumerable<{EntityName}ListItem> FilteredItems =>
        string.IsNullOrWhiteSpace(_searchText)
            ? _items
            : _items.Where(x => /* filter logic on searchable fields */);

    protected override async Task OnInitializedAsync()
    {
        BreadcrumbState.Set(
            new BreadcrumbItem("Home", "/"),
            new BreadcrumbItem("{Page Title}")
        );

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        _items = await {EntityName}Service.GetAll{EntityName}sAsync();
        _loading = false;
    }

    private void OnSearchInput(ChangeEventArgs args)
    {
        _pendingSearchText = args.Value?.ToString() ?? string.Empty;
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            InvokeAsync(() =>
            {
                _searchText = _pendingSearchText;
                StateHasChanged();
            });
        }, null, DebounceMs, Timeout.Infinite);
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
    }

    private void OnRowClick(DataGridRowMouseEventArgs<{EntityName}ListItem> args)
    {
        if (args.Data is not null)
        {
            NavigationManager.NavigateTo($"/{route}/{args.Data.{EntityName}Id}");
        }
    }

    private async Task OnAddClicked()
    {
        var result = await DialogService.OpenAsync<AddEdit{EntityName}Dialog>("Add {EntityName}",
            new Dictionary<string, object>(),
            new DialogOptions
            {
                Width = "500px",
                CloseDialogOnOverlayClick = false,
                CloseDialogOnEsc = true
            });

        if (result is Save{EntityName}Request request)
        {
            try
            {
                var id = await {EntityName}Service.Create{EntityName}Async(request, _currentUserId);
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = "{EntityName} Created",
                    Detail = "Successfully created.",
                    Duration = 4000
                });
                NavigationManager.NavigateTo($"/{route}/{id}");
            }
            catch (Exception ex)
            {
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = "Error",
                    Detail = ex.Message,
                    Duration = 6000
                });
            }
        }
    }
}
```

## Grid Column Patterns

```razor
@* Basic text column *@
<RadzenDataGridColumn TItem="T" Property="Name" Title="Name" />

@* Fixed-width column *@
<RadzenDataGridColumn TItem="T" Property="City" Title="City" Width="180px" />

@* Centered numeric column *@
<RadzenDataGridColumn TItem="T" Property="Count" Title="Count" Width="100px" TextAlign="TextAlign.Center" />

@* Action buttons column *@
<RadzenDataGridColumn TItem="T" Title="Actions" Sortable="false" Width="120px">
    <Template Context="item">
        <RadzenStack Orientation="Orientation.Horizontal" Gap="0.25rem">
            <RadzenButton Icon="edit" Size="ButtonSize.ExtraSmall"
                          Click="@(() => OnEditClicked(item))" />
            <RadzenButton Icon="delete" Size="ButtonSize.ExtraSmall" ButtonStyle="ButtonStyle.Danger"
                          Click="@(() => OnDeleteClicked(item))" />
        </RadzenStack>
    </Template>
</RadzenDataGridColumn>
```

## Delete with Confirmation Dialog

```csharp
private async Task OnDeleteClicked({EntityName}ListItem item)
{
    var confirmed = await DialogService.OpenAsync<DeleteConfirmDialog>("Confirm Delete",
        new Dictionary<string, object>
        {
            { "EntityName", item.Name },
            { "WarningMessage", "This will permanently delete this item." }
        },
        new DialogOptions { Width = "400px", CloseDialogOnOverlayClick = false, CloseDialogOnEsc = true });

    if (confirmed is true)
    {
        var success = await {EntityName}Service.Delete{EntityName}Async(item.{EntityName}Id, _currentUserId);
        if (success)
        {
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Deleted",
                Detail = $"{item.Name} has been deleted.",
                Duration = 4000
            });
            await LoadDataAsync();
        }
    }
}
```

## Rules

- Always implement `IDisposable` when using debounce Timer
- Loading state with `RadzenProgressBarCircular`
- Empty state with `RadzenAlert`
- Search uses client-side filtering with debounced input
- Row clicks navigate to detail page
- Use `@bind-Value` for grid selection (required by Radzen)
- Get current user ID from `AuthenticationStateProvider` in `OnInitializedAsync`
- Set breadcrumbs in `OnInitializedAsync`
