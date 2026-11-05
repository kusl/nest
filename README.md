# nest

A shared notebook for house hunting. Paste a link from Zillow, Redfin, Realtor.com or any site, fill in the details together and leave comments. No accounts, no cookies, no database. Blazor Server on .NET 10, on the [velixir](https://velixir.net) free plan (0.25 vCPU, 256 MB). Live at https://nest.velixir.run/.

- The server keeps the newest 1,000 entries (links, details, comments) in memory and forgets them on restart. Each browser keeps up to 20,000 in IndexedDB and offers back what the server lacks.
- A home is every entry that shares a key with it: Zillow `zpid`, Redfin id, Realtor.com id, normalized street and ZIP, or a hashed link. Different query strings are the same link. Entries from different people and sites merge onto one page; the newest value of each detail wins.
- Dotted border: only browsers still have it.
- No third-party code. Strict CSP with no inline scripts.

## Requirements

- .NET SDK 10.0.100 or later in the 10.0 band
- Node.js 22 or later (JavaScript tests)
- Podman 4 or later (end-to-end tests)

## Commands

```
dotnet run --project src/Nest.Web
scripts/run-all.sh [--skip-e2e] [--skip-js] [--skip-export]
```

`run-all.sh` builds with warnings as errors, runs the .NET and JavaScript tests with coverage, runs Playwright in Podman, then writes `docs/llm/dump.txt`. Output lands in `artifacts/`. Exit code 2 is a usage error, 127 a missing tool.

## Layout

| Path | What |
| --- | --- |
| `src/Nest.Web` | The app. The only folder velixir builds. |
| `src/Nest.Web/wwwroot/js` | Shared modules: keys, fields, merge, storage, sync |
| `tests/Nest.Web.Tests` | xUnit v3 unit and integration tests |
| `tests/Nest.E2E` | Playwright tests, run in Podman |
| `tests/js` | Vitest tests for the hand-written modules |
| `tests/fixtures` | Cases both test suites run, so C# and JavaScript agree |
| `scripts` | Build, test and export scripts |
| `docs/adr` | Architecture decision records |

## Deploy

Every push to `main` runs `.github/workflows/velixir.yml`, which uploads `src/Nest.Web` only. It needs the `VELIXIR_API_KEY` repository secret and the `VELIXIR_APP_ID` repository variable. Every push also runs `.github/workflows/ci.yml`. Turn on the HTTP health probe (`/health`) in velixir.

Limits can be changed without a rebuild through environment variables, for example `Nest__Capacity=2000`. See `src/Nest.Web/Infrastructure/NestOptions.cs`.

## License

AGPL-3.0-or-later. See `LICENSE` and `REUSE.toml`.
