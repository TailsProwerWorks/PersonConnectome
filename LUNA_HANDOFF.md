# PersonConnectome: Luna continuation handoff

Checkpoint: 2026-09-11. Repository: `C:\Coding\PersonConnectome`.

## Start here

Continue with Luna at medium reasoning. Resume the current dirty working tree; do not reset to HEAD, restore deleted files, or restart the entire audit. No commit or push was performed. This handoff records work completed in the preceding conversation; its creation did not change runtime code or rerun the runtime tests.

The user requested a correctness audit, then authorized fixes. The immediate unresolved issue is delayed reactions in the running game, with a large neural backlog and unexplained strong object audio. They now requested this handoff to continue in Luna.

Read the current implementation and nearby documentation before making a small, focused change. Preserve existing architecture, user changes, and unrelated deployment files. Be precise about offline tests versus actual in-game evidence. Do not assure the user that everything is wired correctly based on compilation alone.

## Current outcome and next priorities

The mod builds, deploys, and now loads in People Playground without the previously reported `System.Security` rejection. The latest user screenshot shows a running controller and readable telemetry. It also shows serious overload; real-time neural timing has not been established.

1. Reproduce and measure sustained neural overload, then address delayed processing without hiding counters or blindly raising the work cap.
2. Identify the external audio source producing `sound=0.94`. The screenshot alone cannot establish whether this is a legitimate offscreen source or an ownership/sensing bug.
3. Explain the actual input contributions behind `in=1.00`; the existing healthy-standing fixture does not cover the screenshot's combined inputs.
4. Identify which of the 14 limbs is excluded and why. Do not assume that `capable=13/14` is automatically correct.
5. Complete targeted in-game verification after fixes. The graph is thresholded, so do not describe it as the complete unfiltered connectome.

## Screenshot acceptance evidence

Latest screenshot supplied by the user:
`C:\Users\Shadow\AppData\Local\Temp\codex-clipboard-ad241592-1895-424b-a741-8af7c84ec7ce.png`

This temporary path may disappear. The relevant displayed values were:

```text
STATE: ALERT
SENSE: OBJECT AUDIO 0.94
BODY: hp=1.00 dmg=0.00 pain=0.00 shock=0.00 o2=1.00 brain=ok
ENV: fire=0.00 heat=0.00 near=0.94 sound=0.94 liquid=0.00
LIMBS: 14 capable=13/14 applied=13 walk=-0.20
REQUEST: arm=-0.08/0.08 leg=-0.16/-0.24 head=-0.10 core=-0.12 grip=0.00/0.00
NEURAL: in=1.00 queued=108840 processed=24000 deferred=129790 fired=2607
```

A lone visible person is near the floor; no obvious sounding object is visible in the crop. Offscreen objects remain possible. This is evidence of successful initialization/stepping and readable overhead text in one scene, not proof of all sensing or physics behavior.

There were 153,790 active candidates in this step: 24,000 processed plus 129,790 deferred. `queued` is the pending-target count, a different set/time from deferred work; the two counters are not expected to match.

## Architecture and files to inspect

- **The shipped game runtime is in `Mod/`.** It is independent of the implementation under `src/PersonConnectome`.
- `Mod/PersonConnectome.Mod.csproj` targets `net48` and compiles against the installed People Playground/Unity assemblies.
- `src/PersonConnectome` and its legacy tests target `net10.0`. They are not referenced or deployed by the game runtime.
- `Mod/script.cs`: registration, neural state, spike propagation, work scheduling, decoder.
- `Mod/ConnectomeSensoryRouter.cs`: sensory populations and input strengths.
- `Mod/PeoplePlaygroundPersonAdapter.cs`: actual game-state sensing and control application.
- `Mod/PersonConnectomeLimbController.cs`: limb mapping/control details.
- `Mod/PersonConnectomeController.cs`: update timing, lifecycle, adapter/brain ownership.
- `Mod/PersonConnectomeStatusDisplay.cs`: overhead telemetry.
- `Mod/RuntimeTypes.cs`: game-runtime data contracts.
- `Mod/ConnectomeRuntimeAsset.cs` and `ConnectomeRuntimeAssetReader.cs`: PNG payload decoding, checksum, graph validation.
- `tests/PersonConnectome.Runtime.Tests`: links actual runtime sources; narrow test doubles and `ConnectomeBrainTestHooks.cs`.
- `tests/PersonConnectome.Adapter.Tests`: links actual adapter/controller sources; `GameDoubles.cs`.
- `docs/api-compatibility.md`, `docs/architecture.md`, `docs/manual-game-test.md`, `docs/PROVENANCE.md`: current documented constraints and acceptance checklist.

The active variation is `Person Connectome (Active)`, attached through `Modification.AfterSpawn`. Existing stock Humans are not scanned or mutated. There is no game persistence and no .NET 10 runtime dependency in the deployed mod.

Installed compiler inspection found Roslyn `Microsoft.CodeAnalysis.CSharp 5.0.0` (C# 14 default), CLR v4.0.30319, shipped Mono BCL file version 4.6.57.0, and a netstandard2.0 facade. Modern syntax compiled. This does **not** prove that net48 is the highest framework the game can support; retain the qualified documentation.

## Neural backlog: current implementation

`MaxActivePerStep = 24000`; refractory duration is five future simulation ticks. Controller default neural cadence is 20 Hz, with at most four catch-up steps. These are configured limits, not measured game FPS.

Each brain step advances `simulationTick`, drives sensory populations, processes active neurons, replaces pending state, and builds a motor command. Under overload, `ProcessFairBacklog` sorts active neuron IDs, starts at `backlogCursor`, processes at most 24,000, defers the rest, and rotates the cursor by the budget.

This avoids fixed-tail starvation, but **fairness is not proof of correct timing**. Global simulation time advances even when neurons are deferred. Investigate potential leak, refractory expiry, propagation delay, and stale inputs under sustained overload. Do not label this backlog as intended biological delay.

The router can stimulate broad populations, including up to 2,048 `ol_sensory` neurons, 256 R1-R6 neurons, 64 per supported retina subtype, and LC4/LPLC2/MDN/DNp09 populations. Strong nearby/audio inputs may contribute to activation, but causality has not been measured.

Suggested next work:

1. Add bounded, preferably on-demand source diagnostics for audio/nearby inputs: object identity, collider/physical/audio ownership, distance, active/mute/volume, and clip identity where permitted by public APIs. Report each input's contribution and excluded-limb reason.
2. Build a source-linked reproduction using the screenshot inputs. Run many consecutive ticks and record active/pending/deferred/fired counts, elapsed work time, and allocations where the offline harness permits. Include quiet and sustained stimulation cases.
3. Determine whether overload comes from routing, recurrent propagation, scheduling semantics, or a combination. Choose the smallest justified fix while preserving graph/data truth and bounded game work.
4. Add meaningful sustained-overload and source-attribution regressions. Keep all prior regressions.
5. Validate, deploy, and have the user recompile/restart and spawn a fresh active person so cached controllers/assets do not confuse the result.

`ReadNearby` uses collider `ClosestPoint` from the root origin and a fixed 128-entry buffer, excludes own transforms, and requires a parent `PhysicalBehaviour`. It has no line-of-sight check and may miss candidates in crowded scenes. Audio currently reads external `physical.MainAudioSource`, requires playing, active/enabled, unmuted, and uses volume times surface-distance falloff. It does not measure sample energy or expose source identity in telemetry.

## Fixes already implemented: preserve these

### Neural and sensory

- Replaced active-only refractory countdown with global tick deadlines so refractory state can expire across silence.
- Terminal `Stop()` clears pending, active, fired, scratch, neural arrays/commands and resets the tick/cursor.
- Deferred input now merges/clamps with newly propagated input instead of overwriting it.
- Rotating overload scheduling prevents permanent fixed-tail starvation; the remaining delay issue is described above.
- Telemetry distinguishes pending targets, processed neurons, deferred work, and actual fired neurons. `REQUEST` denotes desired decoder outputs; applied counts/walking come from the adapter.
- Healthy circulation no longer drives full body-motion input; circulation deficit feeds hazard.
- Floor/physical-contact weights are 0.15 each, heartbeat 0.1, sound/velocity 0.6, rotation/balance 0.4. Actual combined inputs can still saturate.
- Retina mapping supports real subtype names: `R7R8_unclear`, `R7_unclear`, `R7d`, `R7p`, `R7y`, `R8_unclear`, `R8d`, `R8p`, `R8y`. Previous exact R7/R8 populations were empty.
- `LiquidHealing` and `NearbyDirection` are explicitly decoder-only; no neural mapping is claimed.

### Adapter and lifecycle

- Installed `InfluenceMotorSpeed(target, influence)` interpolates the current joint speed. Stop now uses `(0, 1)` even when a limb is incapable.
- Missing joints are safe, grips are optional, and stop drops grips even on incapable limbs.
- Motor/grip/chemistry application is suppressed for consciousness <= 0.8, invalid health, terminal state, or freeze > 0.5. `CanDrive` requires a live limb, joint, and `IsCapable`.
- Every read reconciles `Person.Limbs` and inactive/new child limbs; destroyed Unity objects are removed and known detached limbs remain tracked. Own colliders/audio remain excluded after detachment.
- Side mapping uses explicit left/right first, then heuristic front-to-right/back-to-left; unknown side averages. Local X is no longer treated as anatomical side.
- `hasAppliedControl` prevents Stop/Dispose on failed loading or never-applied adapters from clobbering existing native walk/motor/grip state.
- OnDisable resets accumulator/neural state and stops the adapter; OnDestroy disposes adapter and display.
- Regeneration boosts preserve native/external baselines and restore only if the current value is still our assigned value. External writes win.
- Neutral/cancelled adrenaline does not write or repair invalid native adrenaline data.
- Exact normal BLOOD/Human blood is excluded from hazards; GORSE BLOOD is corrosive; KNOCKOUT POISON is sedation.
- `Vibration` was renamed `Impact` and displayed as CONTACT IMPACT, since self-motion can cause floor impacts.
- `Drowning` was renamed `SubmergedHypoxia`, displayed as SUBMERGED HYPOXIA. Oxygen deficit plus a submerged limb does not establish water as the cause. Pure hypoxia is LOW OXYGEN.
- Invalid/nonfinite health yields `HealthValid=false`, `Alive=false`, and INVALID DATA/unknown health rather than a false DEAD label. Scalar/distance guards and zero-horizontal-direction handling were added.
- Sensory labels and values are selected together; brain injury has the correct value, and nearby priority no longer masks object audio.

### Display

The controller owns an unparented, nonphysical world-space label. It follows the current head or root fallback and remains upright/camera-facing without inheriting ragdoll mirroring. The screenshot verifies readability in one scene only. Mirror/head-loss/native rendering scenarios still need testing.

## Critical: the game's suspicious-code rejection

The earlier screenshot reported `Forbidden using directive: System.Security`. The cause was our addition of `using System.Security.Cryptography` for the asset checksum. An offline build alone did not detect game policy rejection.

Official references:

- https://wiki.studiominus.nl/details/shadyCodeRejection.html
- https://wiki.studiominus.nl/intro/boilerplate.html

The prior turn retrieved these through PowerShell `Invoke-WebRequest` (HTTP 200) after the web tool returned 402. Forbidden imports include System.Security and subnamespaces, System.Web, UnityEngine.Networking, Steamworks, and aliases.

Current `Mod/ConnectomeRuntimeAsset.cs` implements SHA256 with ordinary unsigned arithmetic, padded blocks, rotations, constants, and hex formatting. It has no System.Security reference. Preserve integrity checks; do not reintroduce the forbidden dependency, evade it through fully qualified names, disable rejection, or ask the user to accept risk.

Checksum tests cover empty/abc known vectors; deterministic data at lengths 1, 7, 55, 56, 57, 63, 64, 65, 119, 120, 127, 128, 129, 4096 and 1,000,000 against independent .NET SHA256 in tests only; the real payload; and corrupted-payload rejection. The legacy `GameBridgeShadyApiGuard` detects forbidden namespace prefixes, static/global forms, and aliases, with a regression test.

The last deployed scripts were explicitly checked for absence of `System.Security`. Runtime uses in-memory streams/gzip, not file access, BinaryReader, reflection, networking, processes, native interop, direct forces, damage, or liquid injection. Label GameObject creation/destruction is visual lifecycle work.

## Payload truth and integrity

The repository input is `Mod/connectome/malecns-v1.0.flyb.gz`; the deployed carrier is `Mod/connectome/malecns-v1.0.png`.

| Property | Verified value |
| --- | --- |
| Dataset | `male-cns:v1.0` |
| Encoding | FLYB v1, little-endian CSR |
| Neurons | 176,422 |
| Retained connections | 6,287,749 |
| Retina entries | 9,619 |
| Filter | weight >= 5; self-edges removed |
| Metadata synapses_in_edges | 90,296,905 |
| Excluded superclasses | none |
| Compressed bytes | 22,964,094 |
| Uncompressed bytes | 46,555,448 |
| PNG dimensions | 4096 x 1869 |
| PNG bytes | 22,814,663 |

Compressed payload SHA256:
`E33DF182BED7A6F3EA279DAF4790A82B05706D3D41E819A6A80C0473E8C559F3`

PNG SHA256:
`83B599B5422B7BC0732F5BAFA4AABCE3DD67FE5D675F2F553143449B7817DD6B`

Independent Python PNG CRC/scanline decoding and gzip parsing confirmed byte identity with the repository input. The Unity runtime reverses bottom-to-top texture rows when rebuilding RGB bytes. The loader validates SHA, dataset, exact neuron/edge/retina counts, header/CSR/index consistency, and has no fallback graph. `ModAPI.LoadTexture` is inside the error-reporting try/catch.

This is a thresholded graph, **not the full unfiltered released graph**. Replacing it requires a separate data/performance decision; do not silently change or misrepresent the dataset.

## Validation at the last runtime checkpoint

The preceding code-change turn completed these checks successfully:

```powershell
dotnet format PersonConnectome.sln --verify-no-changes --no-restore
dotnet build PersonConnectome.sln -c Release
dotnet run --project tests/PersonConnectome.Tests -c Release --no-build
dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release --no-build
dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release --no-build
git diff --check
.\scripts\Deploy-Mod.ps1
```

Build: zero warnings/errors. Tests: **41 passed** (16 legacy, 9 actual-source runtime, 16 actual-source adapter). Formatter passed. Diff check passed; Git emitted existing LF/CRLF notices. Use `--no-build` only after a fresh build when continuing.

The runtime and adapter harnesses use game doubles. They do not establish actual Unity physics, audible output, frame performance, or all native API behavior. The later user screenshot provides the limited native evidence described above. Use `docs/manual-game-test.md` for health/death/recovery, grip/loco, silence/audio attribution, mirror/head-loss, and single/multiple-person performance checks.

## Deployment checkpoint

Game root:
`C:\Program Files (x86)\Steam\steamapps\common\People Playground`

Managed assemblies: `People Playground_Data\Managed`; compiler: `ppgModCompiler`.

Deployment: `C:\Program Files (x86)\Steam\steamapps\common\People Playground\Mods\PersonConnectome`.

`scripts/Deploy-Mod.ps1` builds the net48 mod and deploys exactly 11 intended files:

```text
mod.json
script.cs
ConnectomeSensoryRouter.cs
ConnectomeRuntimeAsset.cs
ConnectomeRuntimeAssetReader.cs
RuntimeTypes.cs
PersonConnectomeController.cs
PeoplePlaygroundPersonAdapter.cs
PersonConnectomeLimbController.cs
PersonConnectomeStatusDisplay.cs
connectome/malecns-v1.0.png
```

The preceding turn independently verified relative paths and SHA256 equality for all 11 files. The raw `.flyb.gz`, test DLLs, and .NET 10 DLLs were absent. The deployment script removes only the exact known stale raw payload; preserve unrelated files. If adding a runtime source, update the manifest/deployment and verify the complete intended file set again.

## Dirty-tree preservation

At handoff creation, existing changes include `Mod/`, `src/`, tests, solution/config, README, notices, docs and scripts. Numerous runtime/source/test files are untracked. `AGENTS.md` and `src/PersonConnectome/StateAndAdapters.cs` are deleted; these deletions were already present and must not be blindly restored. Much of this checkpoint predates the fixes described above, so do not attribute the whole diff to this audit.

Run `git status --short` and inspect focused diffs before editing. Do not reset, clean, stage everything, commit, or push as part of merely resuming the diagnosis. No new dependency or broad rewrite is needed to start measuring the current issue.

Suggested continuation prompt:

> Read LUNA_HANDOFF.md and continue from the dirty checkpoint. Investigate and fix the delayed neural processing and unexplained object audio using the actual Mod runtime. Preserve existing fixes, measure sustained behavior, run relevant tests, and clearly separate offline validation from in-game evidence.
