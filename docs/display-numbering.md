# Display number reuse - 2026-09-14

The screenshot's `1 controlled` was the current registered-person count. `#16` was a different value: a monotonically increasing identifier assigned during display construction. Deleting/undoing a spawn removed it from the live list but never reclaimed its number.

Display activation now acquires the lowest free positive number. Disable/disposal releases it by owner, so repeated disable/destroy callbacks cannot free a number reassigned to someone else. Existing active people keep their numbers. Construction alone consumes no number. After all displays are removed, the next starts at #1. Re-enabling a removed display can assign a different number if its former one is occupied. Numbers identify currently registered people, not lifetime identities or the live count.

## Files changed for this fix

- Mod/PersonConnectomeStatusDisplay.cs: acquire/release registration numbers instead of incrementing a lifetime counter.
- Mod/TelemetryLayout.cs: small Unity-independent TelemetryIdentityPool used by the actual display.
- tests/PersonConnectome.Runtime.Tests/Program.cs: reuse, stable survivors, repeated cleanup, restoration collisions, empty reset and 100 delete/respawn cycles.
- tests/PersonConnectome.Adapter.Tests/Program.cs: controller disable/destroy/replacement count assertions using the existing narrow display double.
- README.md, Mod/README.txt, docs/architecture.md and docs/manual-game-test.md: numbering semantics and native Undo acceptance.

Existing unrelated working-tree changes were preserved. No asset, neural, sensor or motor behavior changed in this fix.

## Validation

| Command/check | Result |
| --- | --- |
| `dotnet format PersonConnectome.sln --verify-no-changes --no-restore` | Passed, exit 0 |
| `dotnet build PersonConnectome.sln -c Release` | Passed, 0 warnings / 0 errors |
| `dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release` | Passed, 56 scenarios |
| `dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release` | Passed, 67 scenarios |
| `dotnet build Mod/PersonConnectome.Mod.csproj -c Release` | Passed, 0 warnings / 0 errors |
| `scripts/Test-GameCompilation.ps1` | Passed: 12 scripts / 22 exact installed references, syntax checks and both installed semantic scanners |
| PowerShell parser over scripts/*.ps1 | Passed, 6 scripts |
| `scripts/Test-DeployDiscovery.ps1` | Passed, discovery and UTF-8/5000-byte README checks |
| `scripts/Deploy-Mod.ps1 -WhatIf` | Passed, registered Steam discovery and game-install forwarding |
| `git diff --check` | Passed |

An initial test used the asset harness's InvalidDataException-only assertion for an ArgumentNullException; that assertion was corrected, and the complete runtime suite passed. Offline identity-pool tests and controller doubles do not replace the native Ctrl+Z/delete/redo test in manual-game-test.md case 34. Restart the game to load the changed static registry.
