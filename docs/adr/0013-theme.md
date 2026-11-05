# 0013 Theme without a flash

Status: accepted

- Colors are `light-dark()` tokens on `:root`. `data-theme="light"` or `"dark"` on `<html>` forces the scheme; without it the OS setting wins.
- `theme-init.js` is a blocking external script in `<head>` that applies the stored choice before first paint, so the CSP needs no hash.
- `App.razor.js` reapplies the theme on `enhancedload`.
