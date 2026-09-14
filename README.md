# Person Connectome

A People Playground Human variation controlled by a bounded neural simulation using a **MaleCNS v1.0-derived fly connectome**. The bundled graph has **176,422 neurons and 6,287,749 retained connections** (synapse weight ≥5). It is a thresholded derivative, not the full released graph or a biologically complete fly mind.

## Play

1. Copy the contents listed by `Mod/mod.json`, `Mod/README.txt`, the thumbnail and the PNG carrier into `People Playground/Mods/PersonConnectome`, or use the deployment script below.
2. Enable **Person Connectome** with **Shady Code Rejection enabled**.
3. Spawn **Person Connectome (Active)** from **Entities**. Stock Humans are not modified.
4. Use the screen panel at the top center. **Prev / Next** selects a controlled person; its number and world coordinates identify it. New spawns reuse the lowest number freed by deletion, Undo or disabling a controller; existing people keep their numbers. The separate controlled count reports current registered people. **A- / A+** adjusts text size; **Collapse** keeps a small header visible. Drag the title bar to move the panel; drag its bottom-right corner to resize it smaller or larger. Use the mouse wheel or scrollbar within each page to reach all readings. The panel stays within the screen; sizing and position last for this session.

The panel is an opaque screen overlay. World lighting, walls, water, fire, camera rotation and zoom do not participate in its rendering. Native rendering and input interaction still require the [manual game checks](docs/manual-game-test.md).

- **Overview:** body state, local limb eligibility, submitted commands, and requested motor output. A request is not measured movement.
- **Senses:** normalized game readings and derived signals. Most values are 0..1, not HP points, degrees or physical units; raw native adrenaline is explicitly labeled. Unknown readings are explicitly marked where validity is tracked; unavailable effect channels do not create a positive signal.
- **Brain:** neural workload, derived population drives, spikes per processed tick and a soma activity map. The map displays every actual source neuron with finite soma coordinates, and its legend lists every superclass present in the asset. Colored flashes mark neurons that fired in the displayed capture; dark neurons do not establish inactivity. The X/Z projection preserves relative source coordinates and is not a human brain image; nearby positions can share pixels in the raster preview.
- **Stimulation:** per-person manual input override controls with Mixed/Manual only modes, normalized channel strengths, optional direction, continuous or bounded neural-tick pulses, live/effective input diagnostics and actual neural response summaries. See [manual stimulation](docs/manual-stimulation.md).

The panel shows the age of the last input sample, measured control-loop time, and skipped game time. It does not claim zero latency or a guaranteed real-time simulation rate. The display normally refreshes ten times per second of unscaled UI time (resizing can refresh it sooner) and can miss activity between captures; the history records every processed control tick.

## What controls the person

The game supplies health, body, environment and contact data. The adapter normalizes those readings, supported sensory encoders pass selected signals to the single `Mod/RuntimeBrain.cs` implementation and pinned sparse graph, and a heuristic decoder maps actual descending/motor-neuron activity to Human requests.

There is no independent water-paddling oscillator or sensor-only escape command. Motor requests change by at most eight normalized units per elapsed game second (elapsed time capped at 0.25 seconds per step). This damps abrupt reversals; it does not prove effective walking, balance or swimming. Terminal state and unavailable/low consciousness clear motor requests immediately. The Minecraft adaptation now includes injury-event input, regional contact, coarse audio frequency bands, geometric looming/small-object cues, body-rotation input and filtered locomotor/turning readout, alongside the existing light, joint and hot/cold routes. [Adaptation coverage](docs/minecraft-adaptation.md) explains what can transfer to a game Human and what remains unsupported. Their input scales and fly-to-human motor decoder remain engineering choices. See the [exact mapping, measurements and limits](docs/sensory-mapping.md).

The connection weights stay fixed. Recent inputs affect short-term neural state, but the controller does not learn from experience or improve simply by staying alive.

Forward/backward and leg activity use 150 ms filters; turning uses 100 ms. Walking modes use hysteresis and a 250 ms minimum dwell, with immediate neural halt/brake and terminal suppression. An active walking mode has a 0.3 neural request floor; the adapter applies a 0.55 final request floor after modifiers so the native 0.5 gate retains a small decay margin. This is an actuator setting, not a raw neural reading. Filtered activity and the selected mode are displayed separately.

The adapter uses native `DesiredWalkingDirection` and `InfluenceMotorSpeed`. Optional grip APIs remain available, but the brain no longer interprets the fly MN9 feeding neuron as a human hand-grab command: zero grip requests leave an existing hold unchanged, while terminal/disabled cleanup explicitly releases it. Threat telemetry separates native body hazards, effective looming input, raw `DNp01-fired` activity and a qualified `escape-request`. A DNp01 spike alone does not prove danger: escape requires injury, body-hazard or looming evidence within 0.5 game seconds, plus movement permission. This local gameplay gate retains raw neural activity; looming can qualify before the body is harmed. An accepted neural escape also starts a 0.6-game-second native walking burst (smoothed request magnitude 0.7), retaining neural backward intent when present. This is an explicit Human adaptation, not a fly jump or guaranteed retreat from a known shooter. These APIs still execute game movement mechanics; removing native pose-menu actions alone does not establish exclusive neural control of native standing/balance. See [API compatibility](docs/api-compatibility.md) for the verified boundary. Limb names infer head/core/arm/hand/leg/foot roles. Explicit left/right takes priority; front maps to right and back to left as a game-plane convention; unknown sides receive the average channel.

The adapter converts normalized joint requests to degrees per second (default maximum 30), and multiplies walking intent by 2 before clamping to the ordinary -1..1 native request. These are adjustable component settings: `JointSpeedDegreesPerSecond` (0..120) and `WalkingRequestGain` (0..4). The installed game selects walking only at absolute request >=0.5 and decays that request by 1 unit/second; any nonzero usable walk request is floored to 0.55 after hazard scaling to retain a small gate margin. Zero neural intent and disabled gains remain zero. The settings are engineering starting points requiring native tuning, not measured biological gains or a guarantee of walking. Native pose controls still contribute, and native brain damage can randomize applied joint speeds.

Eligibility is local: broken, disconnected, detached or otherwise locally failed limbs stop while healthy connected limbs remain available. Detached limbs remain known-owned for filtering and liquid diagnostics, but their physical, circulation and liquid readings do not control the remaining body. Wetness, submersion and the game's global `IsCapable` flag do not independently disable all limbs. Brain damage and unconsciousness are not death. Native `Braindead`, or finite average health ≤0.001, is terminal. Invalid average health suspends output without resetting the retained neural state or inventing death. Consciousness ≤0.8 or unavailable consciousness suppresses active output while nonterminal neural sampling continues.

Existing restorative requests remain engineered gameplay interventions: bounded regeneration boosts, explicit liquid-driven adrenaline adjustments and fire-intensity reduction. Adrenaline and extinguishing are applied as per-second rates using measured control elapsed time, so changing tick frequency does not multiply their strength for equal-duration runs. Native pain/shock no longer request chemical calming, and sensed adrenaline no longer amplifies itself. They are derived from sensed state, not evidence of neural healing. Regeneration cleanup restores only values still owned by this controller. Control adds no forces, damage/liquid injection, networking, shell execution, native interop or gameplay object spawning/deletion. There is no fallback demo graph. Game save compatibility is not established.

## What it senses—and the limits

- **Body:** native health, pain, shock, oxygen, consciousness, brain damage/death, wounds, blood, circulation, temperature, movement and limb state. Damage and minimum connected-limb health are derived quantities. Blood shows native per-limb min/max amounts and readable coverage; the relative deficit is the maximum connected-limb drop from its observed peak, not total blood lost or a pre-tracking health baseline. Invalid oxygen cannot become hypoxia; missing circulation is unknown.
- **Environment:** water/wetness, fire/burn, lava, acid contact, local ambient temperature and distance-attenuated dynamic-object heat/cold. The default ambient temperature 20 is neutral. Static geometry is excluded from the object-temperature pass. Nearby acid alone is not acid exposure.
- **Motion/contact:** falling uses downward native limb velocity with floor-contact gating. Vibration is a mechanical proxy; joint sensing reads connected native hinge angles/speeds. Signed velocity and tilt are explicitly labeled world/game projections. Known own limbs remain excluded from collision, nearby and audio detection after detachment; foot-floor impacts can still be self-generated contact.
- **Light:** ambient brightness plus a bounded nearby-light proxy for native tubes, bulbs and flashlight/floodlight groups. Enabled state, current sprite color and transformed footprint matter; turning or switching a lamp can change the existing light/ON/OFF input. This is approximate illumination, without exact texture or shadow sampling. See [local light sensing](docs/local-light-sensing.md).
- **Vision:** up to five nearest-visible collider observations in separate angular bands, selected from 16 candidates in a frontal 180-degree field that follows the connected head, including rotation and mirroring. Line of sight and combined ambient/local light determine the signal. Valid collider bounds and relative velocity provide approximate angular size, expansion and motion cues; measured head angular velocity is subtracted from visual sweep and also provides a separate visual-motion proxy under light. Each band contributes to visual feature inputs, with per-neural-side maximum pooling to avoid multiplying drive. Target names are conservative component classifications, not recognition or semantic eyesight. The 128-collider overlap and 16 visibility-query limits can miss objects in crowded scenes; partial searches are labeled. See [head-relative vision and turning](docs/head-relative-vision.md).
- **Audio:** active, unmuted external physical-object playback. Own sources and generic `Root/Root` artifacts are filtered. The strongest accepted source supplies a signed world-horizontal bearing, mapped to annotated left/right auditory neurons. This is an engineering projection, not head-relative localization or general biological hearing. When Unity provides a valid spectrum, the strongest accepted source also supplies coarse below/above-100 Hz energy bands. Empty or invalid spectrum data keeps broad playback detection without guessing a frequency. The Senses page retains the last detected source with real-time seconds since detection; current sound becomes zero when playback stops.
- **Projectiles:** native projectile components and collision/motion evidence. Penetrability alone is not evidence of a projectile.
- **Liquids and syringes:** reads every liquid already present in tracked native circulation, with explicit recognition of all 41 stock IDs in the inspected 1.27.17 game. Senses lists each identity and its highest concentration in a tracked limb. Verified IDs use documented exposure categories; custom/unregistered liquids remain exposure-only telemetry. Internal liquid identity does not stimulate fly taste or smell neurons. Water Breathing Serum is not water, and strength/durability serums do not request adrenaline. Native zombie state, pain, oxygen and other effects are read separately; exposure is not proof an effect happened. These are game-state readings and existing control constraints, not biological chemical recognition. No liquid is injected. See the [liquid mapping and limits](docs/api-compatibility.md#liquid-and-syringe-coverage).

Submerged hypoxia requires both submersion and a valid oxygen deficit; it does not prove that water caused the deficit. Smell, semantic perception and unsupported biological senses are not fabricated.

A newly observed health drop in a connected limb produces a one-sample `damage-event`. An explicitly engineered injury encoder stimulates touch populations; it does not change the native `pain` reading or measure a feeling. Spawn, invalid data, disconnection and recovery re-prime the baseline. An injury occurring entirely between samples may be missed.

The mod does not synthesize random telemetry or neural commands. Mapped readings feed the graph, and the graph feeds a heuristic motor decoder. Apart from the measured health-drop event above, internal health, pain, oxygen, zombie state and chemical identity remain readable telemetry and/or control constraints; they are not assigned invented sensory receptors. For example, `velocity` is native average speed divided by 10; `walk-request` is a submitted direction, so nonzero requests can coexist with zero sensed motion. Every eligible active neuron now receives its input/decay update. Overload means more than 24,000 spikes needed propagation in one tick; excess crossings are reported as dropped-spikes. It is not a frame-time measurement. The tested heavy-input cases no longer drop neural work; native performance still needs checking. See [scheduler measurements](docs/scheduler-performance.md).

## Build and verify

Requires .NET 10 SDK, PowerShell 7 for the offline script checks, and installed People Playground assemblies for the `net48` game project. No NuGet packages are required.

```powershell
dotnet format PersonConnectome.sln --verify-no-changes --no-restore
dotnet build PersonConnectome.sln -c Release
dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release
dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release
dotnet build Mod/PersonConnectome.Mod.csproj -c Release
pwsh -NoProfile -File scripts/Test-ModSourceSafety.ps1
pwsh -NoProfile -File scripts/Test-GameCompilation.ps1
pwsh -NoProfile -File scripts/Test-DeployDiscovery.ps1
.\scripts\Deploy-Mod.ps1 -WhatIf
git diff --check
```

The source safety check parses every manifest script against the [documented rejection rules](https://wiki.studiominus.nl/details/shadyCodeRejection.html), including identifier tokens, forbidden namespaces and aliased imports. It does not disable rejection and does not substitute for a native load test.

The game-compilation check uses the exact assembly references in the installed compiler's `last_instructions` record. Run a mod compilation in the game first if that record is missing. It accepts `-GameInstall` or discovers Steam. This catches API-reference differences that an ordinary project build can miss, including unsupported IMGUI types. It emits only to memory, then runs the installed compiler's low-risk and high-risk semantic rejection scanners. It does not run the full compiler server or load the mod.

Deploy from an appropriately permitted PowerShell:

```powershell
.\scripts\Deploy-Mod.ps1
# Or select an installation explicitly:
.\scripts\Deploy-Mod.ps1 -GameInstall 'D:\Games\People Playground'
```

Discovery uses registered Steam paths and modern or legacy `steamapps/libraryfolders.vdf`. The resolved install is forwarded to MSBuild. `-GameInstall` takes precedence; an invalid explicit path fails rather than silently selecting another installation. `-WhatIf` validates the package and shows the destination/planned build without building or writing the game directory. `-NoBuild` is for an already verified build. Direct `dotnet` builds use the project default Steam directory unless `-p:PeoplePlaygroundInstall='D:\Games\People Playground'` is supplied.

Deployment copies manifest scripts, `mod.json`, a README with the current Git commit marker, the thumbnail and the PNG carrier, then verifies SHA-256. It removes only the known stale raw `.flyb.gz` from older deployments. Other game-directory files are preserved. Rebuild the carrier with `scripts/Build-ConnectomeCarrier.ps1`; the raw payload stays a build input and its identity constants must change deliberately before a different payload is accepted.

## Code and attribution

For the contributor workflow, architecture guardrails, validation commands and recommended reasoning models, see [CONTRIBUTING.md](CONTRIBUTING.md).

- `Mod/RuntimeBrain.cs`: single authoritative sparse LIF simulation and decoder.
- `Mod/PeoplePlaygroundPersonAdapter.cs` / `PersonConnectomeLimbController.cs`: native sensing and local actuation.
- `Mod/PersonConnectomeController.cs`: Unity lifecycle, timing and collision probes.
- `Mod/ConnectomeRuntimeAsset*.cs`: validated texture-carrier/FLYB decoding and shared immutable graph data.
- `Mod/BrainVisualization.cs` / `PersonConnectomeStatusDisplay.cs`: bounded diagnostic sample and screen overlay.
- `tests/`: shipped sources linked against narrow doubles. These verify contracts, not Unity physics/rendering.

[Architecture](docs/architecture.md), [API compatibility](docs/api-compatibility.md), [provenance](docs/PROVENANCE.md) and [manual checks](docs/manual-game-test.md) describe the boundaries.

The Male CNS dataset is attributed under **CC BY 4.0**; see `THIRD_PARTY_NOTICES`. This mod uses the prepared derivative from [fly-brain-minecraft](https://github.com/blendi-remade/fly-brain-minecraft), which also supplies the reference sensory population choices and locomotor readout weights and inspired the diagnostic display. Its millisecond integrator, flight/reflex machinery and physiological validation results are not reproduced here. The visualization here reads the existing asset and actual local runtime activity.

- [Male CNS Connectome project](https://male-cns.janelia.org/)
- [MaleCNS v1.0 downloads](https://male-cns.janelia.org/download/)
- [Pinned derivative asset](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/resources/connectome/malecns-v1.0.flyb.gz)
- [Codex Connectome Data Explorer](https://codex.flywire.ai/) and [FlyWire/Codex background](https://codex.flywire.ai/about_flywire)
- [People Playground modding documentation](https://wiki.studiominus.nl/index.html)
- [Person Connectome source](https://github.com/TailsProwerWorks/PersonConnectome)

Body threat and neural escape remain visible in adjacent header columns, with independent clear/idle/requested states and colors. The native signal and manual-input indicator remain below them; the columns never replace each other.

Stock Pumpkin now supplies labeled nearby/head-contact gameplay cues to real ORN_DM1/DM2/VA2 and LB3b/c populations. Catalog identity is checked by original asset, not a renameable object label. Proximity includes walls; head contact requires an actual collision pair. This adds no measured odor/taste, eating, nutrition or direct food-to-walking behavior. Other items retain existing geometry, motion, contact, light, audio and temperature sensing. See [food, escape and blood checks](docs/food-escape-blood.md).

Hearing includes nearby jukebox music and attached object audio sources, with the same self-source filtering, current playback strength/direction and last-heard history. Global/menu music is outside the nearby-object sensor.
