# 0016 Test strategy

Status: accepted

- .NET: xUnit v3 (`xunit.v3` 4.x) on Microsoft Testing Platform, enabled in `global.json`. Unit tests cover keys, links, fields, entry rules, the merge, the service and infrastructure. Integration tests use `WebApplicationFactory` for pages, headers, cookies, the hub guard and compression.
- JavaScript: Vitest in Node tests the pure exports and the `Nest` sync class with a fake server and store. DOM and IndexedDB are covered by Playwright. Vitest maps `/js/` imports to `wwwroot/js`.
- Shared fixtures in `tests/fixtures` are run by both suites, so C# and JavaScript parse links, addresses and fields, and merge homes, the same way.
- End to end: Playwright .NET in `mcr.microsoft.com/playwright/dotnet:v<version>-noble`. The version comes from `Directory.Packages.props` only. The app is published and run inside the container on localhost with relaxed limits. Tests run serially.
- Coverage is reported (Cobertura) with no threshold.
