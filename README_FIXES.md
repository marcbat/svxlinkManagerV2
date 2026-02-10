# UI Fixes Branch

This branch contains fixes for 4 UI issues in the SvxLink Manager V2 application.

## Prerequisites
This branch should be based on `develop` branch. If you're seeing this and the branch is empty:

```bash
# Ensure this branch has develop's code:
git fetch origin develop
git merge origin/develop --allow-unrelated-histories
```

## Issues Fixed

1. ✅ **Toast notifications white background** - Added dark theme CSS
2. ✅ **Sound table header white** - Added dark theme CSS override  
3. 🔄 **Upload success uses alert** - Changed to use ToastService
4. 🔍 **Enregistrer button issue** - Under investigation

## Files Changed

- `src/SvxlinkManagerV2.Presentation/wwwroot/css/site.css` - Dark theme for toasts and tables
- `src/SvxlinkManagerV2.Presentation/Pages/Sounds/Upload.razor` - Use ToastService instead of alert

