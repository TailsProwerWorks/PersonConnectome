# Food, escape, blood and object audio follow-up - 2026-09-14

This follow-up preserves the current architecture and prior working-tree changes. Native gameplay has not been run by this audit.

## Findings and changes

- Food had no input route. Stock Pumpkin now supplies explicitly labeled gameplay proximity and native connected-head contact cues to exact real populations: ORN_DM1/DM2/VA2 (211 neurons combined) and LB3b/c (34). Original catalog asset identity prevents renamed unrelated objects from qualifying. Cached lookup avoids per-tick catalog fallback searches. Proximity includes walls and does not measure an odor field; contact does not imply eating or nutrition. Other foods are not guessed.
- A qualified DNp01 escape event only set avoidance and could leave idle walking at zero. It now requests a 0.6-game-second native walking burst, magnitude 0.7 before existing smoothing and actuator limits. Existing neural backward intent determines negative direction; otherwise it uses the native forward convention. There is no shooter localization or guaranteed retreat. Halt/brake and unsafe native state stop it. Injury evidence alone never initiates this burst.
- Explicit loss of a previously observed connected limb had erased the observation without producing an injury event. It now emits the last measured health fraction once; previously absent limbs and unsupported/invalid circulation do not fabricate events. Terminal/invalid person state still clears injury input. Actuator-only stops preserve observation history so unconscious/frozen bodies can still be sampled; true suspension/disposal resets it.
- Blood used the correct native method but an ambiguous label and an inaccurate test double. The display now separates original-blood native min/max and readable connected-limb coverage from maximum relative limb deficit since its observed peak. Initially empty circulation has raw zero but unknown deficit; small positive amounts remain valid. Mixed liquids do not masquerade as blood. Invalid distributions are guarded before the native reader. This is not whole-body blood percentage, litres, or a reconstruction of pre-tracking loss.
- Hearing checked only PhysicalBehaviour.MainAudioSource, missing separate music channels. It now checks JukeboxBehaviour.audioSource and AudioSources attached to nearby physical-object hierarchies, retaining playing/mute/ownership/Root filters. At most 64 unique sources are evaluated in the existing 128-collider nearby scan; truncated scans are marked partial. Only the strongest eligible source supplies the single spectrum read and world-horizontal direction. Object-owned child enumeration is a Unity hierarchy query, not a whole-scene sound search.

## Source evidence and limits

Installed game metadata/IL verifies CatalogBehaviour.Spawn assigns SerialiseInstructions.OriginalSpawnableAsset, FindSpawnable returns null for a missing item, and Collider2D.IsTouching is available. Stock asset data contains Pumpkin / The spooky food. Original identity is not semantic vision. Missing catalog identity leaves food unavailable for that controller lifetime.

CirculationBehaviour.GetAmountOfBlood selects Limb.GetOriginalBloodType through BloodContainer.GetAmount(Liquid). Native BloodAmount instead aliases TotalLiquidAmount. ActualBloodLimit has no managed references in the inspected assembly, so it was not adopted as a presumed healthy capacity.

The installed JukeboxBehaviour exposes its own audioSource. Playback strength remains source volume times distance attenuation, not measured sound pressure, Unity listener mixing, wall attenuation, music recognition or a calibrated ear. Coarse spectrum ratios remain the existing approximation. Global/menu music without a nearby physical owner is not sensed.

The upstream [OdorTable](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/entity/OdorTable.java) and [TasteTable](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/entity/TasteTable.java) are reference gameplay conventions. Their pumpkin pie mapping is not a biological signature of this game's raw Pumpkin. MaleCNS attribution, graph weights, neuron/edge counts and carrier are unchanged. The official wiki was unavailable through the browser during this pass; compatibility evidence comes from the installed game surface and exact compiler checks.

## Real-graph checks

Fresh real graph for each case, 40 ticks at 0.05 game seconds; normalized engineered amplitudes, not Hz:

| Case | Observed population spikes | Whole-graph spikes | Dropped | Peak absolute Walk |
| --- | ---: | ---: | ---: | ---: |
| Quiet | 0 | 0 | 0 | 0 |
| Pumpkin nearby (ORN_DM1 readout) | 518 | 135980 | 0 | 0 |
| Pumpkin head contact (LB3b readout) | 77 | 109660 | 0 | 0 |
| One half-health damage event (head-touch readout) | 863 | 129862 | 0 | 0.3 |
| Audio left | 424 | 145677 | 0 | 0.3 |
| Audio right | 363 | 109435 | 0 | 0.3 |

All 28 mapping cases completed. Zero walking for isolated food cues is an observed limitation, not hidden by a food-seeking controller. The damage case does not prove a DNp01 escape; the bounded burst is separately regression-tested using actual firing in an isolated routing fixture. These fixtures test decoder contracts and do not replace the shipped graph.

## Files changed in this follow-up

- src/UI/ManualInputState.cs, Core/SensoryFrame.cs and Core/FlyMotorCommand.cs, Core/LifBrain.cs: food routes, explicit burst state and truthful input/output fields.
- src/Adapters/PeoplePlaygroundPersonAdapter.cs: stock food identity/contact, injury history/loss, blood readings, jukebox/attached audio.
- tests/PersonConnectome.Adapter.Tests/Adapters/GameDoubles.cs and Program.cs; tests/PersonConnectome.Runtime.Tests/Core/Program.cs: focused native-boundary, reset, routing and real-asset checks.
- README.md, assets/README.txt, docs/architecture.md, docs/api-compatibility.md, docs/PROVENANCE.md, docs/sensory-mapping.md, docs/minecraft-adaptation.md, docs/manual-game-test.md and this report: behavior, attribution and limitations. Existing unrelated edits, including thumb.png, are preserved.

## Validation

| Command/check | Final result |
| --- | --- |
| dotnet format PersonConnectome.slnx --verify-no-changes --no-restore | PASS, exit 0 |
| dotnet build PersonConnectome.slnx -c Release | PASS, 0 warnings, 0 errors |
| dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release | PASS, 58 scenarios |
| dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release | PASS, 72 scenarios |
| dotnet build src/PersonConnectome.Mod.csproj -c Release | PASS, 0 warnings, 0 errors |
| pwsh -NoProfile -File scripts/ai/Test-GameCompilation.ps1 | PASS, 12 scripts against 22 exact installed compiler references; documented syntax and both installed semantic scanners accept |
| PowerShell parser, scripts/**/*.ps1 | PASS, 6 scripts; no scripts changed in this follow-up |
| pwsh -NoProfile -File scripts/ai/Test-DeployDiscovery.ps1 | PASS, discovery and 5000-byte UTF-8 README boundary checks |
| pwsh -NoProfile -File scripts/deploy/Deploy-Mod.ps1 -WhatIf | PASS, discovered installed game forwarded to planned MSBuild |
| dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release -- --mapping-report | PASS, 28 cases |
| git diff --check | PASS, exit 0; existing LF/CRLF conversion warnings only |
| Local Deploy-Mod.ps1 -GameInstall <registered installed game> -NoBuild | PASS, all 16 source/deployed hashes verified; Workshop identity 3800353718 preserved |

Initial regression runs caught test setup assumptions (asset initialization order and blood-double fixtures), which were corrected. A validation build overlapped another build of the same project and hit Windows file locking; the isolated rerun passed. The README deployment check caught CRLF-expanded text above 5000 bytes; prose was shortened without removing license/attribution, and the boundary check then passed.

## Manual acceptance still required

Use docs/manual-game-test.md cases 35-38: stock/renamed Pumpkin and real head contact; qualified escape from idle with healthy/missing limbs and mirrored people; blood draining/mixed liquids; jukebox/radio toggling, distance and source ownership. Also check narrow-panel wrapping and native head turning. The head already accepts graph-derived turning requests while walking is idle, and vision follows the native head pose. Food/audio do not guarantee turning, and no random scanning, learning or food-seeking autopilot was introduced. Build and test-double results do not prove gait, audible native playback, render correctness or full in-game loading.

Installed README is 4945 bytes, below the 5000-byte native loader limit. Deployment embeds base commit 4dfaaef7312b; that marker does not identify the current uncommitted changes. No Workshop upload, commit or push was performed. Restart the game to refresh its cached mod metadata/source.
