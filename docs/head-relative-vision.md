# Head-relative vision and turning - 2026-09-14

Previously head movement could receive neural requests, but visual selection and direction ignored head orientation. The nearest physical collider was chosen before checking visibility, so a blocked or rearward object could prevent another visible object from being sampled. Visual sweep also omitted observer angular velocity.

## Adaptation

The reference [WorldSenses](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/entity/WorldSenses.java) measures objects in the fly's orientation frame; [FlyBody](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/entity/FlyBody.java) applies turning to body/head orientation. FlyBody also has optional random exploration and odor-steering fallbacks. Those are explicitly hand-built behavior, not connectome output, and have not been imported.

For the stock People Playground Human, the connected brain limb's transformed local +X axis defines facing. TransformVector includes actual rotation and scale/mirroring. A frontal 180-degree field is an explicit 2D gameplay choice, not measured human or fly visual anatomy. Missing/disconnected brain anchors and invalid/degenerate facing yield no object-vision signal; the root fallback is not invented eyesight. Custom anatomy or a visually flipped sprite that does not mirror its transform may not match this convention and needs native testing.

The existing 128-collider overlap still supplies proximity, temperature, sound and local-light observations independently of gaze. The nearest 16 eligible in-field collider surfaces are kept in distance order in reused arrays. Five 36-degree angular bands each retain their nearest unobstructed candidate. The adapter can inspect all 16 candidates; further candidates within an already observed band are skipped. A close center object therefore does not erase an approaching object in a different band. Known own colliders are excluded. A filled 32-hit linecast is rejected conservatively; a full overlap, candidate truncation or filled linecast marks vision-scan=partial. Objects beyond those limits may be missed. The light proxy remains diffuse illumination, not a directional retina or brightness-at-target measurement.

Each band's signed head bearing, divided by 90 and clamped, supplies the existing visual encoder's side weighting. Expansion, looming and small-motion features are evaluated per observed band. Their L and R drives are separately pooled by maximum, avoiding duplicated stimulation while preserving simultaneous observations on both sides. The equivalent amplitude=max(L,R), direction=(R-L)/amplitude passes through the existing manual resolver once per channel. Positive counterclockwise bearing in the 2D world is projected onto R population annotations; negative onto L. This is a declared engineering mapping of a 2D angle onto fly L/R labels, not anatomical yaw equivalence or a calibrated target-tracking controller. The legacy world-X visual direction remains diagnostic only. Unknown/invalid head bearing yields equal bilateral input when a separate supported visual feature is supplied; the native adapter supplies no object-vision feature without a valid view.

Angular sweep subtracts the native head rigidbody angular velocity from target-relative orbital angular velocity. Pure head rotation can produce sweep but cannot produce looming expansion. Bounds/radial motion still supply the existing expansion/approach estimates. Deformation, object rotation, exact silhouettes, rendered optical flow and semantic recognition are not reconstructed. Closest collider surfaces define field eligibility, so objects crossing a field edge may be missed even when part of their silhouette would be visible.

The single authoritative LifBrain and MaleCNS-derived asset remain intact. DNa02/DNg13/DNa01 activity already supplies filtered head/core requests through native InfluenceMotorSpeed; the next sample observes the resulting head transform. No force, transform rotation, random scan, fake spike or direct sensor-to-head controller is added. These connections enable feedback but do not establish effective gaze tracking or guaranteed spontaneous exploration. Native joint torque/limits still determine motion.

REQUEST (WALK IDLE) now explicitly names walking mode: head requests may exist while walking is idle. Senses exposes head-facing-world-deg, target-bearing-head-deg, vision-scan and five view-band strengths (CW2/CW1/front/CCW1/CCW2). The original nearest-target geometry remains diagnostic; live visual encoders use the spatial frame. Zero-strength/dark bands do not count as observed. Observations are value snapshots so later reads cannot mutate earlier frames. Requests remain separate from observed pose.

## Files and verification

- src/Adapters/Person/PeoplePlaygroundPersonAdapter.cs: head frame, bounded visible-target selection, sweep and telemetry.
- src/Core/SensoryFrame.cs + src/Core/FlyMotorCommand.cs: head observation/validity fields.
- src/Core/LifBrain.cs: visual side weighting and walking-mode label.
- tests/PersonConnectome.Adapter.Tests/Adapters/Program.cs and GameDoubles.cs: rotation/mirroring, invalid/missing head, target visibility, budgets and relative sweep.
- tests/PersonConnectome.Runtime.Tests/Core/Program.cs: head-relative input traverses a deliberately isolated test graph into a stationary turning request, with direction precedence and decay checks. The small graph is a test fixture only; shipped data are unchanged.
- README.md, assets/README.txt, architecture/API/sensory-mapping/Minecraft-adaptation/provenance/manual-game-test docs: current behavior and limits.

Native acceptance is manual-game-test.md case 32. Offline tests do not simulate Unity neck physics, validate custom prefab facing, demonstrate target tracking or measure in-game frame time.

### Offline validation results

| Command/check | Result |
| --- | --- |
| `dotnet format PersonConnectome.slnx --verify-no-changes --no-restore` | Passed, exit 0 (initial test-initializer formatting corrected) |
| `dotnet build PersonConnectome.slnx -c Release` | Passed, 0 warnings, 0 errors |
| `dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release` | Passed, 55 scenarios |
| `dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release` | Passed, 67 scenarios |
| `dotnet build src/PersonConnectome.Mod.csproj -c Release` | Passed, 0 warnings, 0 errors |
| `scripts/ai/Test-GameCompilation.ps1` | Passed, 12 scripts against 22 exact game compiler references, syntax checks and both installed semantic scanners |
| PowerShell parser over scripts/**/*.ps1 | Passed, 6 scripts |
| `scripts/ai/Test-DeployDiscovery.ps1` | Passed, discovery and UTF-8/5000-byte README boundary checks |
| `scripts/deploy/Deploy-Mod.ps1 -WhatIf` | Passed, registered Steam discovery and game-install forwarding |
| Mod `--mapping-report` | Passed, 26 real-graph synthetic cases, zero dropped spikes; original cases match the existing report and the two-band case has 329 LC4 spikes / 141,902 whole-graph spikes |
| `git diff --check` | Passed |

Existing geometry tests were updated for head-relative sweep and explicit brain anchors. An initial stationary-head test expectation was corrected to account for native motor influence (0.18 of the requested target in the double); this is not proof of real neck displacement. No in-game screenshots, physics acceptance or native performance measurement were obtained.

The surroundings extension adds SpatialSurroundings and SpatialVisualInputs regression cases for simultaneous bands, occlusion, darkness, frame snapshots, per-side pooling, duplicate-drive avoidance and manual-only suppression. Neural weights remain fixed: short-term activity is not experience-based learning.

A read-only correctness review found no actionable issues in either the head-frame change or the five-band extension. Both stages were deployed locally after validation, preserving the matching Workshop creator identity. The final deployment verified all 16 files against source hashes and the unchanged connectome carrier byte-for-byte. No Workshop upload or git commit was made; the embedded HEAD marker remains 4dfaaef7312b and does not identify these uncommitted changes.
