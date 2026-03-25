---
name: form-dialog
description: Generate an Add/Edit form dialog component using Radzen forms. Use when creating a dialog for creating or editing an entity.
argument-hint: "[EntityName]"
---

Create a new Add/Edit form dialog for: $ARGUMENTS

## What to Generate

Generate an Add/Edit dialog component that follows the project's exact Radzen form pattern.

### Dialog Component — `src/FairwayFinder.Web/Components/Pages/{Domain}/Dialogs/AddEdit{EntityName}Dialog.razor`

Follow this exact structure:

```razor
@using FairwayFinder.Features.Data

<RadzenTemplateForm TItem="Save{EntityName}Request" Data="_model" Submit="OnSubmit">
    <RadzenStack Gap="1rem">

        @* Each field follows this pattern: *@
        <RadzenStack Gap="0.25rem">
            <RadzenText TextStyle="TextStyle.Caption" style="color: var(--rz-text-secondary-color);">Field Label</RadzenText>
            <RadzenTextBox Name="FieldName" @bind-Value="_model.FieldName" Style="width: 100%;" />
            <RadzenRequiredValidator Component="FieldName" Text="Field is required." />
        </RadzenStack>

        @* Error display *@
        @if (!string.IsNullOrEmpty(_error))
        {
            <RadzenAlert AlertStyle="AlertStyle.Danger" Shade="Shade.Lighter" AllowClose="false" Size="AlertSize.Small">
                <RadzenText TextStyle="TextStyle.Body2" class="rz-m-0">@_error</RadzenText>
            </RadzenAlert>
        }

        @* Action buttons — always at bottom *@
        <RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.End" Gap="0.5rem">
            <RadzenButton Text="Cancel" ButtonStyle="ButtonStyle.Light"
                          Click="@(() => DialogService.Close(null))" ButtonType="ButtonType.Button" />
            <RadzenButton Text="@(_isEdit ? "Save Changes" : "Create {EntityName}")" Icon="@(_isEdit ? "save" : "add")"
                          ButtonStyle="ButtonStyle.Primary" ButtonType="ButtonType.Submit"
                          IsBusy="_submitting" BusyText="Saving..." />
        </RadzenStack>
    </RadzenStack>
</RadzenTemplateForm>

@code {
    [Inject] public DialogService DialogService { get; set; } = default!;

    /// <summary>
    /// If provided, the dialog is in edit mode with pre-populated values.
    /// </summary>
    [Parameter] public Save{EntityName}Request? Existing { get; set; }

    private Save{EntityName}Request _model = new();
    private bool _isEdit;
    private bool _submitting;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Existing is not null)
        {
            _isEdit = true;
            _model = new Save{EntityName}Request
            {
                // Copy all fields from Existing
            };
        }
    }

    private void OnSubmit()
    {
        _error = null;
        _submitting = true;

        try
        {
            DialogService.Close(_model);
        }
        catch (Exception ex)
        {
            _error = $"Unexpected error: {ex.Message}";
            _submitting = false;
        }
    }
}
```

## How to Open From a Page

The calling page opens the dialog and handles the service call:

```csharp
// ADD
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
    var id = await {EntityName}Service.Create{EntityName}Async(request, _currentUserId);
    NotificationService.Notify(new NotificationMessage
    {
        Severity = NotificationSeverity.Success,
        Summary = "{EntityName} Created",
        Detail = $"Successfully created.",
        Duration = 4000
    });
}

// EDIT — pass Existing parameter
var result = await DialogService.OpenAsync<AddEdit{EntityName}Dialog>("Edit {EntityName}",
    new Dictionary<string, object> { { "Existing", existingRequest } },
    new DialogOptions
    {
        Width = "500px",
        CloseDialogOnOverlayClick = false,
        CloseDialogOnEsc = true
    });

if (result is Save{EntityName}Request request)
{
    await {EntityName}Service.Update{EntityName}Async(request, _currentUserId);
    NotificationService.Notify(new NotificationMessage
    {
        Severity = NotificationSeverity.Success,
        Summary = "{EntityName} Updated",
        Detail = $"Changes saved.",
        Duration = 4000
    });
    await LoadData();
}
```

## Radzen Input Components by Type

| Data Type | Component |
|---|---|
| string | `RadzenTextBox` |
| string (multiline) | `RadzenTextArea` |
| int / decimal | `RadzenNumeric` |
| bool | `RadzenCheckBox` or `RadzenSwitch` |
| DateTime | `RadzenDatePicker` |
| enum / select | `RadzenDropDown` |
| email | `RadzenTextBox` + `RadzenEmailValidator` |

## Validators

- `RadzenRequiredValidator` — required fields
- `RadzenEmailValidator` — email format
- `RadzenLengthValidator` — min/max length
- `RadzenCompareValidator` — field comparison (e.g., confirm password)
- All validators need `Component="FieldName"` matching the input's `Name`

## Rules

- Dialog returns the DTO on submit, `null` on cancel — the **calling page** handles the service call
- Use `RadzenStack` for all layout, never raw divs
- Labels use `TextStyle.Caption` with `var(--rz-text-secondary-color)`
- All inputs: `Style="width: 100%;"`
- Cancel button: `ButtonType="ButtonType.Button"` (prevents form submit)
- Submit button: `ButtonType="ButtonType.Submit"` with `IsBusy` + `BusyText`
- Only create the Dialogs/ subfolder if it doesn't exist yet
