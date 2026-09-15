# Scheduler follow-up - 2026-09-14

The old scheduler charged the same 24,000-neuron budget for cheap voltage integration/decay and expensive outgoing spike propagation. Under sustained input it discarded many updates below firing threshold, including decaying stored state. Sorting and selecting that larger active set also added work. This was intentional truncation, but it lost useful neural state and produced persistent OVERLOADED reports.

## Change

Every eligible active cell now integrates its current signed input and decay once, before any spike propagates. The existing threshold, clamps, five intervening refractory ticks and one-tick transmission delay remain. The second phase services up to 24,000 firing events, including cells with no outgoing edges. Only excess threshold crossings require sorting and fair selection; fresh sensory candidates retain priority. Rejected crossings are reset and counted as dropped-spikes, without erasing new next-tick input sent to them by accepted spikes. They are never replayed as an old backlog.

The active set is bounded by the loaded graph's 176,422 neurons. The firing cap limits events, not edges or CPU time; a cell's outdegree varies. This is a scheduling-policy change toward the existing untruncated discrete model, not a new biological model. Formerly truncated neural traces and motor requests can change. The authoritative runtime and real MaleCNS carrier are unchanged in identity; no fake controller or fabricated stimulation was added.

`integrated`/`input-integrated` counts updates with nonzero net input; `decay-only` counts the remaining eligible updates. `fired/24000` counts serviced firing events. `dropped-spikes` counts rejected threshold crossings. STEADY means no crossings were rejected in that tick; it does not promise a frame rate. These dropped counts have different units from the old unintegrated-neuron counter and must not be compared as a percentage reduction.

Repeated target insertions in the outgoing loop are also avoided when a pending entry already exists, and the source's transmitter sign is read once per firing event. The source/target summation remains real signed graph data.

## Matched offline benchmark

Command: `dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release -- --benchmark`.

Same machine, .NET 10 Release, same real graph and synthetic combined-input frame; 30 warm-up ticks followed by 120 measured ticks per run, three fresh-brain runs per implementation. Graph decoding and warm-up are excluded from the table. Edge counts sum outgoing rows of accepted firing cells, including edges skipped for refractory targets. Allocated bytes are current-thread allocations during measurement, not retained memory/RAM. They include small measurement overhead.

| Metric | Previous integration cap | New firing-event cap |
|---|---:|---:|
| Mean tick time across runs | 3.91-4.01 ms | 5.36-5.66 ms |
| p95 tick time across runs | 6.03-6.14 ms | 7.53-8.04 ms |
| Maximum tick across runs | 7.44-10.59 ms | 9.25-13.38 ms |
| Peak outgoing edges traversed/tick | 404,159 | 418,366 |
| Peak serviced firing events/tick | 9,021 | 15,137 |
| Input integrations per 120 ticks | 2,880,000 | 6,548,533 |
| Old unintegrated updates discarded | 2,957,955 | Not applicable: all eligible updates serviced |
| New threshold crossings discarded | Not separately measured | 0 |
| Allocated bytes per 120 ticks | 5,652,576-5,697,120 | 32,000-65,280 |

The improvement is retained neural information and lower allocation, not lower CPU time. Mean/p95 cost increased. The tested graph workload stayed below the new firing cap; deliberate synthetic cap-overflow tests exercise its fallback. Native Unity/Mono, cold loading, several people and unusual stimulation can cost more. The residual-only prototype was also measured, but retained fewer inputs and still dropped work; it was not kept.

## Rendering and membership follow-up

The post-freeze performance pass keeps graph and scheduler semantics unchanged.
The Brain page now indexes each displayed soma by neuron ID and walks only the
current fired-neuron list (at most 24,000 entries) when painting flashes, instead
of probing all 141,781 displayed points through a hash set on every map refresh.
The runtime also uses a dense per-neuron fired marker for repeated population and
map membership checks. On the matched .NET 10 benchmark, neural ticks improved
from roughly 5.8-6.4 ms mean with small per-run allocations to 4.9-5.2 ms mean
with zero measured tick allocations; this is still not a Unity Mono frame-time
guarantee. The map optimization is most visible when the Brain page is selected.
Native sensing, physics and UI layout remain CPU/main-thread work.

## Multi-person cadence follow-up

Each enabled controller keeps its configured neural tick rate, but the active
controllers are assigned evenly distributed fixed-step phases. Phases are
rebalanced when the enabled population changes, so this does not assume a fixed
number of people. This prevents people spawned together from running their
sensor scan, neural step and motor application in the same physics callback. It
smooths frame-time bursts; it does not reduce the total CPU work or change the
per-person tick rate. The installed game must be restarted after deployment
before measuring multi-person behavior.

All 25 sensory mapping scenarios were rerun with the new scheduler and completed with zero dropped crossings. The refreshed input/whole-graph spike counts are in [sensory-mapping.md](sensory-mapping.md). Quiet input remained silent. No biological understanding or reliable human gait follows from these results.

## Regression coverage

The runtime suite now has 53 scenarios. Added checks cover 30,000 subthreshold inputs without truncation, exact residual decay and cutoff, fresh input among residuals, zero-net-input cancellation, 24,001 actual firing candidates with sensory priority, current-state clearing without stale replay, next-tick arrivals, and an independent 80-tick synchronous reference comparing per-neuron voltage, pending input and spikes. Existing refractory recovery, terminal/invalid state, signed accumulation and real-graph determinism tests pass.

Older overload tests now use threshold-crossing inputs, so they continue exercising real truncation. They were not simply removed to make the new policy pass.

## Files changed in this follow-up

- src/Core/LifBrain.cs: two-phase integration/firing scheduler, counters and outgoing-loop work.
- src/UI/StatusDisplay.cs: explicit integration/decay/dropped-spike labels.
- tests/PersonConnectome.Runtime.Tests/Core/Program.cs and LifBrainTestHooks.cs: regressions and benchmark instrumentation.
- README.md and assets/README.txt: player explanation of the new limit.
- docs/architecture.md, api-compatibility.md, PROVENANCE.md, minecraft-adaptation.md, sensory-mapping.md, manual-game-test.md and this file: current semantics, measurements and native checks.
- docs/AUDIT-2026-09-14.md: marks the earlier sweep's scheduler results as historical.

Pre-existing user/accuracy-sweep changes remain in the working tree. No commit, push or Workshop upload is part of this change.

## Native acceptance

Restart/reload the mod, then test quiet/ordinary scenes, sustained combined inputs, manual stimulation, hazards, pause/resume and multiple people. Compare integrated + decay-only, fired/24000, dropped-spikes, measured loop time and skipped game time. Above 24,000 active/input cells alone should no longer cause overload. Verify actual movement and frame-time spikes; the old heavily truncated neural trace is not an expected movement oracle. See manual-game-test.md case 30.

## Focused runtime performance pass - 2026-09-15

The next pass keeps the full graph and configured neural cadence. `LifBrain` now
uses dense per-neuron value/presence arrays with reusable sparse ID lists for
pending input, active cells, priority input and scheduled firing candidates.
This removes per-tick dictionary/hash-set work without changing integration,
threshold, refractory or propagation rules. The People Playground adapter caches
component discovery (with topology and newly-added-component invalidation),
refreshes limb discovery periodically, and continues reading live component
state each sample. Telemetry no longer hides every row before rebuilding the
same page; UI refresh cost is measured separately. `VisionRadius` is pushed to
the live adapter before every sensor read, so inspector changes take effect
without respawning.

The controller now reports sensor, brain, actuator and UI milliseconds in the
telemetry header. The offline six-person benchmark warms six real-graph brains
for 30 ticks, then measures 120 ticks with the same combined synthetic sensory
frame:

`BENCH sixPeople=6 meanSixMs=19.50 meanPerPersonMs=3.25 p95SixMs=23.01 maxSixMs=25.19 allocatedBytes=0 processed=39290778 fired=3672780 dropped=0`

This is .NET 10 source-linked runtime evidence for six neural steps per tick,
not a native Unity sensor/physics/UI frame-time guarantee. Use the in-game
bucketed telemetry to identify which main-thread phase dominates a real scene.

## Validation and local deployment

All listed checks exited 0:

| Command/check | Result |
|---|---|
| dotnet format PersonConnectome.slnx --verify-no-changes --no-restore | Pass |
| dotnet build PersonConnectome.slnx -c Release | 0 warnings, 0 errors |
| dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release | 53 scenarios passed |
| dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release | 58 scenarios passed |
| dotnet build src/PersonConnectome.Mod.csproj -c Release | 0 warnings, 0 errors |
| pwsh -NoProfile -File scripts/ai/Test-GameCompilation.ps1 | 12 scripts / 22 installed compiler references; documented-rule guard and both installed semantic scanners passed |
| PowerShell Language.Parser | All 6 scripts parsed |
| pwsh -NoProfile -File scripts/ai/Test-DeployDiscovery.ps1 | Discovery and README byte-limit tests passed |
| pwsh -NoProfile -File scripts/deploy/Deploy-Mod.ps1 -WhatIf | Registered Steam discovery and MSBuild install forwarding passed |
| git diff --check | Pass |

Local deployment with explicit discovered GameInstall and -NoBuild verified all 16 deployed files against expected hashes. The PNG carrier stayed byte-identical and CreatorUGCIdentity 3800353718 was preserved. The embedded Git marker remains 4dfaaef7312b, the existing HEAD rather than a new commit of these working-tree changes. No Workshop upload was performed. Native gameplay was not run.
