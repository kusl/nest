# 0012 No cookies

Status: accepted

Blazor issues an antiforgery cookie on pages that host interactive components. Nothing posts forms to the server, so `CookielessAntiforgery` replaces `IAntiforgery`: it issues no tokens and rejects unsafe form posts. Data protection keys are in memory. With no cookies there is no consent banner.
