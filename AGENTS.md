# Repository Guidelines

## Project Structure

- `Mod/` contains the authoritative People Playground runtime and its `net48` project.
- `Mod/RuntimeBrain.cs` is the single authoritative brain implementation, compiled directly by the mod and linked by the runtime test harness.
- `tests/PersonConnectome.Runtime.Tests/` and `tests/PersonConnectome.Adapter.Tests/` link shipped runtime sources against narrow game doubles.
- `Mod/connectome/` contains the canonical raw FLYB build input and PNG carrier. `docs/` records architecture, API limits, manual-game checks, and provenance; `scripts/` builds the carrier, deploys the mod, and inspects installed APIs.

## Build, Test, and Development Commands

From the repository root:

```powershell
dotnet format PersonConnectome.sln --verify-no-changes --no-restore
dotnet build PersonConnectome.sln -c Release
dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release
dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release
```

Build the game-facing project with `dotnet build Mod/PersonConnectome.Mod.csproj -c Release`. Rebuild the PNG carrier with `.\scripts\Build-ConnectomeCarrier.ps1`; deploy with administrator PowerShell using `.\scripts\Deploy-Mod.ps1`.

## Coding Style and Naming

Use four spaces, braces on their own lines, nullable annotations where the project already enables them, `PascalCase` for public types/members, and `_camelCase` for private fields. Keep the game-facing code in `Mod/` and do not add forbidden game imports such as `System.Security` to mod scripts.

## Testing Guidelines

Add focused regression coverage with every behavior change. Keep game API doubles narrow and distinguish test-double proof from native Unity/People Playground evidence. Run both test projects plus formatting and `git diff --check` before publication. Native changes require the applicable checklist in `docs/manual-game-test.md`.

## Commits and Pull Requests

Use concise imperative commit subjects, for example `Publish PersonConnectome runtime and connectome asset`. Pull requests should explain behavior and compatibility impact, list validation commands and results, identify asset/provenance changes, and include native-game screenshots or logs when UI, physics, loading, or performance is affected. Do not commit generated deployment directories or unrelated local state.
