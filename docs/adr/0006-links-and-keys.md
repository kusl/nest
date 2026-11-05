# 0006 Reading listing links and addresses

Status: accepted

- The first `http(s)://` URL in the pasted text is used, so text shared from an app works. The URL ends at the first character outside printable ASCII, a space, `"`, `'`, `<` or `>`; trailing `.,;:!?)]}` are dropped. At most 2,048 characters.
- Query strings and fragments are dropped everywhere. Hosts are lowercased; user info in the host is rejected.
- Zillow: `<digits>_zpid` anywhere in the path. Redfin: `/home/<digits>`. Realtor.com: `_M<digits>-<digits>`. Leading zeros are dropped. These links are stored as `https://` with the original host and path.
- Other links: `url:` plus the 64-bit FNV-1a hash of host without `www.` or port plus the path without trailing slashes. Scheme does not count.
- Addresses: ASCII letters are lowercased, everything that is not an ASCII letter or digit separates words, common street words are shortened to USPS forms (`street` → `st`, `route` → `rte`, `north` → `n`, …). `apt`, `apartment`, `unit`, `suite`, `ste` and `#` start the unit; an explicit unit field wins. Key: `address:<zip>-<street words>[-unit-<unit words>]`, at most 100 characters.
- Non-ASCII letters are separators, not folded. Rare in US addresses; it keeps C# and JavaScript identical without Unicode tables.
- Regular expressions avoid case-insensitive flags and use `\z` in .NET, because the engines differ on Unicode case folding and on `$` before a newline.
- The browser also guesses an address from Zillow, Redfin and Realtor.com paths to prefill the details form. Zillow slugs mix street and city, so that guess is marked for checking. Guesses are never saved without the user pressing Save.
- `tests/fixtures/links.json` and `addresses.json` are run by both test suites.
- Nest never fetches the listing sites. There is no API access, and fetching would add third-party requests and terms-of-use questions.
