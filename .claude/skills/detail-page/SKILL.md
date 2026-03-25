---
name: detail-page
description: Generate a detail/view page for an entity with edit and delete actions. Use when creating a page that shows full entity details with route parameter.
argument-hint: "[EntityName]"
---

Create a new detail/view page for: $ARGUMENTS

## What to Generate

### Detail Page — `src/FairwayFinder.Web/Components/Pages/{Domain}/Pages/{EntityName}Detail.razor`

Follow this exact structure:

```razor
@page "/{route}/{Id:long}"
@rendermode InteractiveServer
@attribute [Authorize]
@using System.Security.Claims
@using FairwayFinder.Features.Data
@using FairwayFinder.Features.Services.Interfaces
@using FairwayFinder.Web.Components.Pages.{Domain}.Dialogs
@using FairwayFinder.Web.Components.Shared.Dialogs
@using FairwayFinder.Web.Components.Shared.Layout.Breadcrumb
@inject I{EntityName}Service {EntityName}Service
@inject AuthenticationStateProvider AuthenticationStateProvider
@inject NavigationManager NavigationManager
@inject NotificationService NotificationService
@inject DialogService DialogService
@inject BreadcrumbState BreadcrumbState

<PageTitle>@(_detail?.Name ?? "{EntityName} Detail")</PageTitle>

@if (_loading)
{
    <RadzenStack AlignItems="AlignItems.Center" class="rz-p-8">
        <RadzenProgressBarCircular ShowValue="false" Mode="ProgressBarMode.Indeterminate" />
    </RadzenStack>
}
else if (_detail is null)
{
    <RadzenAlert AlertStyle="AlertStyle.Warning" Shade="Shade.Lighter" AllowClose="false">
        {EntityName} not found.
    </RadzenAlert>
}
else
{
    <RadzenStack Gap="1.5rem" class="rz-mx-auto rz-p-4" style="max-width: 1000px;">
        @* Header with edit/delete actions *@
        <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" JustifyContent="JustifyContent.SpaceBetween">
            <RadzenText TextStyle="TextStyle.H4" class="rz-m-0">@_detail.Name</RadzenText>
            <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem">
                <RadzenButton Text="Edit" Icon="edit" ButtonStyle="ButtonStyle.Primary" Size="ButtonSize.Small"
                              Click="OnEditClicked" />
                <RadzenButton Text="Delete" Icon="delete" ButtonStyle="ButtonStyle.Danger" Size="ButtonSize.Small"
                              Click="OnDeleteClicked" />
            </RadzenStack>
        </RadzenStack>

        @* Detail card *@
        <RadzenCard class="rz-p-4">
            <RadzenStack Gap="0.75rem">
                @* Field display pattern *@
                <RadzenStack Gap="0.125rem">
                    <RadzenText TextStyle="TextStyle.Caption" style="color: var(--rz-text-secondary-color);">Field Label</RadzenText>
                    <RadzenText TextStyle="TextStyle.Body1">@_detail.FieldValue</RadzenText>
                </RadzenStack>
            </RadzenStack>
        </RadzenCard>

        @* Optional: Related data grid *@
        @if (_detail.RelatedItems.Any())
        {
            <RadzenCard class="rz-p-4">
                <RadzenStack Gap="1rem">
                    <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" JustifyContent="JustifyContent.SpaceBetween">
                        <RadzenText TextStyle="TextStyle.H6" class="rz-m-0">Related Items</RadzenText>
                        <RadzenButton Text="Add" Icon="add" Size="ButtonSize.ExtraSmall"
                                      ButtonStyle="ButtonStyle.Primary" Click="OnAddRelatedClicked" />
                    </RadzenStack>

                    <RadzenDataGrid Data="@_detail.RelatedItems" TItem="RelatedDto" AllowSorting="true">
                        <Columns>
                            <RadzenDataGridColumn TItem="RelatedDto" Property="Name" Title="Name" />
                            <RadzenDataGridColumn TItem="RelatedDto" Title="Actions" Sortable="false" Width="100px">
                                <Template Context="item">
                                    <RadzenButton Icon="delete" Size="ButtonSize.ExtraSmall"
                                                  ButtonStyle="ButtonStyle.Danger"
                                                  Click="@(() => OnDeleteRelatedClicked(item))" />
                                </Template>
                            </RadzenDataGridColumn>
                        </Columns>
                    </RadzenDataGrid>
                </RadzenStack>
            </RadzenCard>
        }
    </RadzenStack>
}

@code {
    [Parameter] public long Id { get; set; }

    private {EntityName}DetailResponse? _detail;
    private bool _loading = true;
    private string _currentUserId = string.Empty;

    protected override async Task OnParametersSetAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        BreadcrumbState.Set(
            new BreadcrumbItem("Home", "/"),
            new BreadcrumbItem("{Entity Plural}", "/{list-route}"),
            new BreadcrumbItem("Detail")
        );

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        _detail = await {EntityName}Service.Get{EntityName}DetailAsync(Id);
        _loading = false;
    }

    private async Task OnEditClicked()
    {
        var existing = new Save{EntityName}Request
        {
            {EntityName}Id = _detail!.{EntityName}Id,
            // Map fields from _detail to request
        };

        var result = await DialogService.OpenAsync<AddEdit{EntityName}Dialog>("Edit {EntityName}",
            new Dictionary<string, object> { { "Existing", existing } },
            new DialogOptions
            {
                Width = "500px",
                CloseDialogOnOverlayClick = false,
                CloseDialogOnEsc = true
            });

        if (result is Save{EntityName}Request request)
        {
            var success = await {EntityName}Service.Update{EntityName}Async(request, _currentUserId);
            if (success)
            {
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = "Updated",
                    Detail = "Changes saved.",
                    Duration = 4000
                });
                await LoadDataAsync();
            }
        }
    }

    private async Task OnDeleteClicked()
    {
        var confirmed = await DialogService.OpenAsync<DeleteConfirmDialog>("Confirm Delete",
            new Dictionary<string, object>
            {
                { "EntityName", _detail!.Name },
                { "WarningMessage", "This action cannot be undone." }
            },
            new DialogOptions { Width = "400px", CloseDialogOnOverlayClick = false, CloseDialogOnEsc = true });

        if (confirmed is true)
        {
            var success = await {EntityName}Service.Delete{EntityName}Async(Id, _currentUserId);
            if (success)
            {
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = "Deleted",
                    Duration = 4000
                });
                NavigationManager.NavigateTo("/{list-route}");
            }
        }
    }
}
```

## Field Display Patterns

```razor
@* Text field *@
<RadzenStack Gap="0.125rem">
    <RadzenText TextStyle="TextStyle.Caption" style="color: var(--rz-text-secondary-color);">Label</RadzenText>
    <RadzenText TextStyle="TextStyle.Body1">@_detail.Value</RadzenText>
</RadzenStack>

@* Nullable field with fallback *@
<RadzenStack Gap="0.125rem">
    <RadzenText TextStyle="TextStyle.Caption" style="color: var(--rz-text-secondary-color);">Label</RadzenText>
    <RadzenText TextStyle="TextStyle.Body1" style="@(string.IsNullOrEmpty(_detail.Value) ? "color: var(--rz-text-disabled-color);" : "")">
        @(_detail.Value ?? "Not specified")
    </RadzenText>
</RadzenStack>

@* Badge/status field *@
<RadzenBadge Text="@_detail.Status" BadgeStyle="@GetBadgeStyle(_detail.Status)" />
```

## Rules

- Use `OnParametersSetAsync` (not `OnInitializedAsync`) for pages with route parameters
- Three states: loading spinner, not-found alert, main content
- Detail fields use Caption label + Body1 value pattern
- Edit opens the same AddEdit dialog with `Existing` parameter
- Delete uses shared `DeleteConfirmDialog`, then navigates back to list
- Set breadcrumbs with link back to the list page
- Wrap content in `max-width` container for readability
