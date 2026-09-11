# Repository Guidelines

## Project Structure

- `Mod/` contains the authoritative People Playground runtime and its `net48` project.
- `src/PersonConnectome/` contains the game-independent graph, simulator, controller, persistence, and domain code targeting `net10.0`.
- `tests/PersonConnectome.Tests/` contains dependency-free contract tests; `tests/PersonConnectome.Runtime.Tests/` and `tests/PersonConnectome.Adapter.Tests/` link the shipped runtime sources against narrow game doubles.
- `Mod/connectome/` contains the canonical raw FLYB build input and PNG carrier. `docs/` records architecture, API limits, manual-game checks, and provenance; `scripts/` builds the carrier, deploys the mod, and inspects installed APIs.

## Build, Test, and Development Commands

From the repository root:

```powershell
dotnet format PersonConnectome.sln --verify-no-changes --no-restore
dotnet build PersonConnectome.sln -c Release
dotnet run --project tests/PersonConnectome.Tests -c Release
dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release
dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release
```

Build the game-facing project with `dotnet build Mod/PersonConnectome.Mod.csproj -c Release`. Rebuild the PNG carrier with `.\scripts\Build-ConnectomeCarrier.ps1`; deploy with administrator PowerShell using `.\scripts\Deploy-Mod.ps1`.

## Coding Style and Naming

Use four spaces, braces on their own lines, nullable annotations where the project already enables them, and file-scoped namespaces in `src/`. Prefer clear small methods, immutable records for persisted data, `PascalCase` for public types/members, and `_camelCase` for private fields. Preserve the separation between `net10.0` library code and `net48` game-facing code. Do not add forbidden game imports such as `System.Security` to mod scripts.

## Testing Guidelines

Add focused regression coverage with every behavior change. Keep game API doubles narrow and distinguish offline proof from native Unity/People Playground evidence. Run all three test projects plus formatting and `git diff --check` before publication. Native changes require the applicable checklist in `docs/manual-game-test.md`.

## Commits and Pull Requests

Use concise imperative commit subjects, for example `Publish PersonConnectome runtime and connectome asset`. Pull requests should explain behavior and compatibility impact, list validation commands and results, identify asset/provenance changes, and include native-game screenshots or logs when UI, physics, loading, or performance is affected. Do not commit generated deployment directories or unrelated local state.
