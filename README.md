# Person Connectome

`Person Connectome` is a **small, deterministic, safety-first neural-control demonstration** for People Playground. It is not a biological connectome and does not claim whole-brain fidelity. Default configuration is disabled and observe-only.

## Layout

- `Mod/`: People Playground metadata and a loadable `Mod.Mod.Main` registration. It adds a separate **Person Connectome Observer** Human variation and attaches only an inert marker component.
- `src/PersonConnectome/`: pure C# simulator, controller, persistence, safety and optional adapters.
- `tests/PersonConnectome.Tests/`: dependency-free offline console tests with a game-facing stub.
- `config/`: versioned sample configuration.
- `docs/`: architecture and manual game verification checklist.

## Safety model

The controller is disabled by configuration; `SafeMotorGate.ObserveOnly` is true by default; `EmergencyDisable()` zeroes every command. Outputs are smoothed and finite-clamped. The controller invokes an explicit `Action<MotorCommand>` only when observe-only is disabled, the emergency latch is clear, and a reviewed adapter reports a capability. The shipped People Playground script reports no motor capability. It never reflects into mutating APIs, applies force, damages, spawns, deletes, uses networking, shells, or saves outside the caller's chosen local state path.

Supported abstract outputs are attention, approach/avoid, left/right, locomotion, reach/grab, flee, freeze, seek energy, and rest. A game bridge must translate only reviewed, supported operations; this release ships no forceful bridge.

## Sensors and effects

`SensoryFrame` reserves bounded channels for existence/alive, body motion/orientation/balance/contact/grounded, nearby direction/LOS/light, touch/pressure, injury and dismemberment, thermal/fire, electric/stun/knockout, impact/fall/acceleration, air/drowning, need/fatigue, sound/vibration, material/projectile context, and reward/aversive/novel/internal signals.

Chemical/status channels are distinct: wetness/water, blood, toxicity/poison, corrosion/acid, sedation/knockout/anesthetic, healing/regeneration, stimulation/adrenaline/syringe/serum, infection/zombie, and immortality/death prevention. These are **approximations**, not medical or API guarantees. `EffectAliasRegistry` is extensible and records bounded unknown names from a public `Effects` collection for local telemetry. Missing or changed game members become zero rather than errors.

## Build and test

Requires .NET 8 SDK; no NuGet packages are required:

```sh
dotnet format PersonConnectome.sln --verify-no-changes
dotnet build PersonConnectome.sln -c Release
dotnet run --project tests/PersonConnectome.Tests -c Release
```

The game was not launched in this sandbox. Before distributing, follow [the manual checklist](docs/manual-game-test.md) and [the API compatibility notes](docs/api-compatibility.md). The package is loadable only after the target People Playground build accepts the documented `ModAPI.Register`/`Modification` registration shown in `Mod/script.cs`; this must be verified in-game.

## Future data

The demo graph is five labelled nodes only. Future datasets must be separately licensed, documented, normalized into `Neuron`/`Synapse`, bounded by the config maxima, and validated offline. Do not represent a dataset import as full-connectome fidelity.
