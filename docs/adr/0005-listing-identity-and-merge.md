# 0005 Listing keys, union-find merge, newest value wins

Status: accepted

- A home has no id of its own. It is the set of keys its entries carry: `zillow:<zpid>`, `redfin:<id>`, `realtor:m<id>`, `address:<zip>-<street>` and `url:<hash>` (ADR 0006).
- An entry's keys are the key derived from its own content (the link, or the street and ZIP of a fact) plus the keys of the home it was added to, at most 8, sorted.
- Merge: union-find over keys. Two entries that share any key belong to the same home, directly or through others. The result does not depend on order, so every browser and the server agree on the same entries.
- The page address is the key with the highest rank (Zillow, Redfin, Realtor.com, address, link, then text order). Any key of the home opens the same page.
- Details: for each field, the value from the entry with the newest id wins. An empty value clears the field. The previous different value is shown as "was …".
- Comments are listed oldest first. Links are shown once per key, newest URL.
- An address key is derived only from a fact that has both street and ZIP. Saving any address field sends all five so the key is consistent.
- Merging cannot be undone. A wrong link or address joins two homes for everyone. Accepted: there are no accounts to decide who may split them.
- The same algorithm exists in C# (`ListingIndex`, for the prerender) and JavaScript (`merge.js`). `tests/fixtures/listings.json` pins both to the same result.
