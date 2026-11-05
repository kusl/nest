# 0017 Dependencies and versions

Status: accepted

- Runtime: none beyond the ASP.NET Core shared framework. No CSS or JavaScript library.
- Tests: `xunit.v3`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Testing.Extensions.CodeCoverage`, `Microsoft.Playwright`, `vitest`, `@vitest/coverage-v8`.
- NuGet versions are central in `Directory.Packages.props`; npm versions are exact with a lockfile.
- `global.json` pins `10.0.100` with `latestFeature`.
- Dependabot opens weekly grouped updates for NuGet, npm and GitHub Actions.
