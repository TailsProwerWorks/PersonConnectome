# Repository Guidelines

## Project Structure

- `src/` contains the authoritative People Playground runtime and its `net48` project.
- `src/Core/LifBrain.cs` is the single authoritative brain implementation, compiled directly by the mod and linked by the runtime test harness.
- `tests/PersonConnectome.Runtime.Tests/` and `tests/PersonConnectome.Adapter.Tests/` link shipped runtime sources against narrow game doubles.
- `assets/connectome/` contains the canonical raw FLYB build input and PNG carrier. `docs/` records architecture, API limits, manual-game checks, and provenance; `scripts/` builds the carrier, deploys the mod, and inspects installed APIs.

## Build, Test, and Development Commands

From the repository root:

```powershell
dotnet format PersonConnectome.slnx --verify-no-changes --no-restore
dotnet build PersonConnectome.slnx -c Release
dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release
dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release
```

Build the game-facing project with `dotnet build src/PersonConnectome.Mod.csproj -c Release`. Rebuild the PNG carrier with `.\scripts\build\Build-ConnectomeCarrier.ps1`; deploy with administrator PowerShell using `.\scripts\deploy\Deploy-Mod.ps1`.

## Coding Style and Naming

Use four spaces, braces on their own lines, nullable annotations where the project already enables them, `PascalCase` for public types/members, and `_camelCase` for private fields. Keep the game-facing code in `src/` and do not add forbidden game imports such as `System.Security` to mod scripts.

Keep the source tree readable and reusable: place Unity-free simulation contracts and the LIF engine under `src/Core/`, body-specific sensing and actuation under `src/Adapters/`, optional HUD/visualization/manual controls under `src/UI/`, and lifecycle/registration wiring under `src/Mod/`. New files should use descriptive names and avoid combining unrelated responsibilities into a monolith.

## Testing Guidelines

Add focused regression coverage with every behavior change. Keep game API doubles narrow and distinguish test-double proof from native Unity/People Playground evidence. Run both test projects plus formatting and `git diff --check` before publication. Native changes require the applicable checklist in `docs/manual-game-test.md`.

## Commits and Pull Requests

Use concise imperative commit subjects, for example `Publish PersonConnectome runtime and connectome asset`. Pull requests should explain behavior and compatibility impact, list validation commands and results, identify asset/provenance changes, and include native-game screenshots or logs when UI, physics, loading, or performance is affected. Do not commit generated deployment directories or unrelated local state.
