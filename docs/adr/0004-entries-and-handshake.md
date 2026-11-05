# 0004 Entries, UUIDv7 ids and the handshake

Status: accepted

- Everything users add is an immutable entry: `link` (a URL), `fact` (one or more detail fields) or `comment` (text). Each carries the keys of its home (ADR 0005). Nothing is edited or deleted; a newer fact replaces an older value.
- The server mints every id with `Uuid7.After` from its own clock, so ids are unique, increasing and sortable as strings. Clients never mint ids, so adding needs a connection.
- The server keeps the newest 1,000 entries (`Nest__Capacity`).
- Handshake, in this order:
  1. The browser calls `Join`. The server sends a snapshot of ids only, plus its capacity.
  2. The browser fetches the entries it lacks, 50 ids per call.
  3. The browser offers entries the server lacks, newest first, at most the capacity, and only ones newer than the server's oldest when the server is full. Offers are sent in chunks under 24 KB of JSON.
- An offered entry is accepted only if its id is a valid UUIDv7, not in the future, older than the server's start, and the entry is already in normal form (rebuilding it gives the same entry). The server keeps the newest 1,000 and its own copy on id ties. Offers beyond the capacity per circuit are ignored.
- The start-time rule rejects forged recent entries: anything stamped after the server started came from this server, so if the server does not have it, it is forged or already evicted.
- A link whose key the server already has on a link entry returns `exists` and the home's key instead of a new entry.
- The same comment or fact on the same keys as one of the 20 newest entries is refused as a repeat.
- Interop uses `JsonElement` arguments and results with internal records, so no wire type is public.
