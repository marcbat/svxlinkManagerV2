# Implementation Plan - UI Fixes

## Current Status
Working on branch `copilot/fix-enregistrer-button-action` which was branched from `main` (empty).
Need to base fixes on `develop` branch code.

## Files Retrieved from GitHub API (develop branch)
✅ AddEditForm.razor - Complete content
✅ Upload.razor - Complete content  
✅ Manage.razor (Sounds) - Complete content
✅ ToastNotification.razor - Complete content
✅ site.css - Complete content
✅ ToastService.cs - Complete content

## Issues & Fixes

### 1. Toast Background Color (White in Dark Theme) ✅ FIXED
**File**: `src/SvxlinkManagerV2.Presentation/wwwroot/css/site.css`
**Fix**: Added CSS rules for `.toast` and `.toast-body` with dark theme colors
```css
.toast {
    background-color: var(--dark-bg-card) !important;
    border-color: var(--dark-border) !important;
}
.toast-body {
    background-color: var(--dark-bg-card) !important;
    color: var(--dark-text) !important;
}
```

### 2. Sound Upload Confirmation (Alert instead of Toast)  
**File**: `src/SvxlinkManagerV2.Presentation/Pages/Sounds/Upload.razor`
**Lines to change**: ~336 and ~378-385
**Current**: Uses `JSRuntime.InvokeVoidAsync("alert", message)`
**Fix**: Use `ToastService.ShowSuccess("Son uploadé avec succès !")` directly
- Remove `ShowSuccessToast()` method (lines 378-385)
- Change line 336 from `_ = ShowSuccessToast(...)` to `ToastService.ShowSuccess(...)`

### 3. Sound List Table Header (White in Dark Theme) ✅ FIXED  
**File**: `src/SvxlinkManagerV2.Presentation/wwwroot/css/site.css`
**Fix**: Added CSS override for `.table-light` class
```css
.table-light, .table-light > th, .table-light > td {
    background-color: rgba(255, 255, 255, 0.05) !important;
    border-color: var(--dark-border) !important;
    color: var(--dark-text) !important;
}
```

### 4. "Enregistrer" Button Not Working
**File**: `src/SvxlinkManagerV2.Presentation/Pages/Salons/AddEditForm.razor`
**Status**: NEEDS INVESTIGATION
**Observations from code review**:
- Button type="submit" is correct (line ~322)
- EditForm has `OnValidSubmit="HandleSubmit"` (line ~44)
- HandleSubmit method exists and looks correct (lines ~402-466)
- Form has DataAnnotationsValidator

**Possible causes**:
1. Form validation preventing submission
2. Button disabled state (_isSaving || _isReadOnly)
3. Model validation errors
4. Missing required fields

**Next steps**: Need to test actual behavior or add debug logging

## Files Created So Far
✅ site.css - with dark theme fixes for toasts and tables

## Next Actions
1. Create Upload.razor with toast fix
2. Test/investigate AddEditForm button issue
3. Commit all changes
4. Create PR from this branch to develop
