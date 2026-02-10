# UI Fixes - Patch Instructions

## Important Note
This branch was created from `main` (empty branch). It needs to be based on `develop` instead.

**Recommended action**: Merge `develop` into this branch before applying these patches.

```bash
git merge origin/develop --no-ff
```

## Patches to Apply

### Patch 1: Toast Dark Theme Fix
**File**: `src/SvxlinkManagerV2.Presentation/wwwroot/css/site.css`
**Location**: After line 221 (after dropdown-item styles)
**Add these lines**:

```css
/* Toast notifications - Dark theme fix */
.toast {
    background-color: var(--dark-bg-card) !important;
    border-color: var(--dark-border) !important;
}

.toast-body {
    background-color: var(--dark-bg-card) !important;
    color: var(--dark-text) !important;
}
```

**And replace the existing `.table-light` style** (around line 167):

```css
/* Override Bootstrap's table-light class for dark theme */
.table-light, .table-light > th, .table-light > td {
    background-color: rgba(255, 255, 255, 0.05) !important;
    border-color: var(--dark-border) !important;
    color: var(--dark-text) !important;
}
```

### Patch 2: Upload Success Toast
**File**: `src/SvxlinkManagerV2.Presentation/Pages/Sounds/Upload.razor`

**Change 1** - Line ~336 in `UploadSound()` method:
```csharp
// OLD:
_ = ShowSuccessToast("Son uploadé avec succès !");

// NEW:
ToastService.ShowSuccess("Son uploadé avec succès !");
```

**Change 2** - Remove lines ~378-385 (the entire `ShowSuccessToast` method):
```csharp
// DELETE THIS METHOD:
private async Task ShowSuccessToast(string message)
{
    try
    {
        await JSRuntime.InvokeVoidAsync("alert", message);
    }
    catch
    {
        // Ignore si JS n'est pas disponible
    }
}
```

### Patch 3: Enregistrer Button Investigation
**File**: `src/SvxlinkManagerV2.Presentation/Pages/Salons/AddEditForm.razor`

**Status**: Under investigation. The code appears correct:
- Button is `type="submit"` (correct)
- Form has `OnValidSubmit="HandleSubmit"` (correct)
- Validation is configured (DataAnnotationsValidator)

**Possible debugging steps**:
1. Add `OnInvalidSubmit="HandleInvalidSubmit"` to the EditForm
2. Add logging in HandleSubmit to confirm it's called
3. Check browser console for JavaScript errors
4. Verify all required fields have values before submission

**Suggested debugging code to add**:
```csharp
private void HandleInvalidSubmit()
{
    Console.WriteLine("Form validation failed");
    Console.WriteLine($"Model state - Name: {_model.Name}, Host: {_model.Host}");
}
```

And in HandleSubmit, add at the start:
```csharp
Console.WriteLine("HandleSubmit called");
```

## Testing Checklist
After applying patches:
- [ ] Toast notifications have dark background
- [ ] Sound upload shows toast instead of alert  
- [ ] Sound table header is dark themed
- [ ] Enregistrer button triggers form submission

