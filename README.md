# Person Connectome

`Person Connectome` is a small, deterministic neural-control demonstration for People Playground. It is not a biological connectome and does not claim whole-brain fidelity.

## Install and attach

1. In a disposable People Playground install, copy this repository's `Mod/` directory to `People Playground/Mods/PersonConnectome`.
2. Start the game and enable **Person Connectome** in the mod list.
3. From **Entities**, spawn **Person Connectome (Active)**. This variation is the explicit attachment mechanism: it is a normal Human with `PersonConnectomeController` attached on spawn. Existing stock Humans are never silently modified.
4. The controller is active by default. Press **F7** for its runtime overlay. Press **F8** at any time to latch the emergency stop; it immediately clears walking. Remove and re-spawn the variation to re-arm it.

The public component fields on the spawned variation are configuration: active control and restorative/chemical outputs are enabled by default, while `ActiveControl = false` and `EnableChemicalOutputs = false` remain explicit diagnostic opt-outs. Tick rate, smoothing, vision radius, and keybinds are visible in the inspector. No persistence is attempted: saved game object/component serialization is game-version-dependent, and preserving an emergency latch across saves would be surprising.

## Active control

The engine takes bounded sensory values on a capped fixed tick (default 20 Hz, maximum four catch-up ticks), feeds a five-neuron leaky integrate-and-fire demonstration, then decodes bounded motor commands. The game adapter uses documented People Playground members for its primary control path:

- `PersonBehaviour.DesiredWalkingDirection` for left/right locomotion;
- `LimbBehaviour.InfluenceMotorSpeed` for per-limb motor control/reaching posture;
- `GripBehaviour` for grab/drop, probed independently so its absence never disables locomotion;
- bounded restorative outputs (`BloodRegenerationPerSecond`, `RegenerationSpeed`, adrenaline and lower fire intensity) are enabled by default; set `EnableChemicalOutputs = false` for the explicit diagnostic opt-out.

It does **not** add force, damage, spawn, delete, use networking/shells/native interop, or inject harmful chemicals. The emergency stop and `ActiveControl = false` prevent game mutation. Optional capabilities fail closed independently; a grip or chemistry mismatch leaves documented walking/limb control available.

## Sensory and status mapping

The adapter samples Person/Limb/Circulation/Physical state and per-limb collision probes: health/damage, blood loss, pain, shock/electric charge, oxygen/suffocation, consciousness/unconsciousness, adrenaline, limbs/dismemberment/joint stress, movement/rotation, contact/touch/impact/vibration, nearby entities/line direction, ambient light, active audio, fire, temperature/hot/cold, wetness/water, blood, liquid identity (acid/corrosion, poison/toxin, sedation/anesthetic, healing/regeneration, stimulation/adrenaline, and unknown materials), and zombie/infection. Missing optional material names become bounded telemetry rather than exceptions.

The API does not provide a stable public general-purpose vision raycast/line-of-sight, disease severity, drug concentration, or selected-stock-Human attachment API. Nearby detection is a bounded `Physics2D.OverlapCircleAll` approximation; light is Unity ambient light; audio is physical-object audio playback; liquid/drug mappings use documented liquid identity strings. See [API compatibility](docs/api-compatibility.md) for exact limitations.

## Build and offline verification

Requires .NET 8; no NuGet packages are needed:

```sh
dotnet format PersonConnectome.sln --verify-no-changes
dotnet build PersonConnectome.sln -c Release
dotnet run --project tests/PersonConnectome.Tests -c Release
git diff --check
```

The game script compiles inside People Playground, not this .NET project. Offline tests cover the pure engine, fixed scheduler, safety latch, persistence validation, capability-style reflection, and a source contract for the active game bridge. Follow [the manual game checklist](docs/manual-game-test.md) before release.

## Layout

- `Mod/script.cs`: loadable People Playground entrypoint, attachment component, bounded runtime brain, and game adapter.
- `src/PersonConnectome/`: game-independent engine, controller, persistence, and capability-reference adapter.
- `tests/PersonConnectome.Tests/`: dependency-free offline/contract tests.
- `config/`: versioned example settings; game-facing fields are configured on the spawned component.

The demo graph is five labelled nodes only. Any future data source must be separately licensed, bounded, and must not be represented as a whole connectome.
