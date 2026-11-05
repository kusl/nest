# 0008 IndexedDB, drafts and clearing

Status: accepted

- Database `nest`, store `entries`, key path `id`, values are entries plus `mine: true` for entries posted from this browser.
- Schema changes are numbered migrations in `wwwroot/js/store.js`; `onupgradeneeded` runs every step from the stored version. A database from a newer schema (`VersionError`) is deleted and recreated, since the server and other browsers can refill it. Other tabs close their connection on `versionchange`.
- Cap: 20,000 entries. After each write the oldest are deleted with one key-range delete and dropped from memory.
- All entries are loaded into memory once per page load to build the home list. 20,000 short entries fit comfortably on a phone.
- Unsent comments are kept per home in `localStorage` (`nest-draft:<key>`) until sent. The "only homes I added to" filter is `nest-mine-only`. The theme is `nest-theme`.
- Solid border: the server has the entry. Dotted: only browsers have it. A home card is dotted when none of its entries are on the server.
- The About page deletes the database, the drafts and the filter, keeps the theme, and empties the in-memory copy. It does not touch the server or other browsers. The next visit fills the database from the server.
- Without IndexedDB the site still works, without history.
