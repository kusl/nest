# 0002 Static SSR, one sync island, views in JavaScript

Status: accepted

- Every page is static server-side rendering. Navigation uses Blazor enhanced navigation.
- `/` (home list) and `/homes/{provider}/{token}` (one home) each host one `InteractiveServer` island, `SyncIsland`. It renders nothing; it is the transport between the server and the browser. `/about` and the not-found page open no circuit.
- The home list is the front page because adding a link is the main task; a static landing page would cost phone users a tap.
- The server prerenders its own view of the list or the home so the page is useful before JavaScript runs. The view modules (`Pages/HomesPage.razor.js`, `Pages/ListingPage.razor.js`) then render the merge of server and local data. They wait for the first reconciliation or 3 s before replacing the prerendered list, and keep it while offline with nothing stored locally.
- Forms are plain HTML driven by the view modules, not Blazor forms: their values depend on data only the browser has.
- `App.razor.js` mounts the view for the element with `data-view` on load and on `enhancedload`, and disposes the previous one. The view root is `data-permanent` with a per-page id, so enhanced navigation does not overwrite JavaScript-owned markup and two different homes never share one.
- Shared logic is in plain modules under `wwwroot/js` (`keys`, `fields`, `merge`, `store`, `sync`), served at `/js/`. One `Nest` instance per document holds the entries, so the island and the view see the same state.
- Reusable Razor components: `ListingCard`, `CommentItem`, `FactList`, `FieldInput`.
- No bUnit: components are thin; logic lives in services and modules tested directly, and pages are covered by integration and Playwright tests.
