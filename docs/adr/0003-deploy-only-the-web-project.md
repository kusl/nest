# 0003 Deploy only the web project

Status: accepted

velixir builds with a Nixpacks-style plan: `dotnet restore` and `dotnet publish -c Release -o out` at the upload root, then starts `./out/<first csproj>`. Uploading the repository root would also build the test projects, including the Playwright driver.

- `velixir.yml` runs `velixir deploy --app "$VELIXIR_APP_ID" --source-dir ./src/Nest.Web`. The API key is the `VELIXIR_API_KEY` secret; the app id is the `VELIXIR_APP_ID` repository variable, so the workflow holds no project-specific value.
- `Nest.Web.csproj` is self-sufficient: target framework, nullable, implicit usings and runtime settings are in the project. It has no package references.
- The web root is set to `wwwroot` next to the assembly when it exists. The app binds to `PORT` when set.
- Deploys are not gated on CI. A failing velixir build keeps the previous release; the `/health` probe stops a release that does not start.
