# 0007 Details fields and their normal form

Status: accepted

| Field | Normal form |
| --- | --- |
| Price, HOA per month, taxes per year | Whole dollars. `$`, commas, spaces, `_` ignored. `450k`, `1.25m` accepted. At most 11 digits. |
| Bedrooms | 0–99 |
| Bathrooms | 0–99 with up to two decimals, trailing zeros dropped |
| Square feet | 1–10,000,000 |
| Year built | 1600–2100 |
| Status, type | One of a fixed list, matched ignoring ASCII case |
| State | Two letters, uppercased |
| ZIP code | Five digits; ZIP+4 is cut to five |
| Lot size, parking, heating and cooling, MLS number, street, unit, city | Text: control characters removed, spaces collapsed, cut to 20–120 characters |

- The browser validates before sending and shows the reason next to the field. The server applies the same rules and refuses an entry with any invalid value. `tests/fixtures/fields.json` pins both.
- Only changed fields are sent, except the address group (ADR 0005).
- Values are self-reported and unverified. There is no history editing.
- Comments: up to 4,000 UTF-16 code units, cut on grapheme boundaries, three or more blank lines collapsed, emoji kept.
