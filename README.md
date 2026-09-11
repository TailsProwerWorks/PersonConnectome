# Person Connectome

`Person Connectome` is a bounded People Playground Human controller driven by the prepared MaleCNS v1.0 connectome. It is a computational model, not a claim of consciousness or complete biological fidelity.

## Install and attach

1. In a disposable People Playground install, run `.\scripts\Deploy-Mod.ps1` from an administrator PowerShell. It copies only the manifest-listed scripts, `mod.json`, and the PNG carrier to `People Playground/Mods/PersonConnectome`.
2. Start the game and enable **Person Connectome** in the mod list.
3. From **Entities**, spawn **Person Connectome (Active)**. This variation is the explicit attachment mechanism: it is a normal Human with `PersonConnectomeController` attached on spawn. Existing stock Humans are never silently modified.
4. The controller samples the person when the validated asset and adapter are available. Motor and chemistry requests are suppressed for invalid health, terminal state, consciousness at or below 0.8, or a freeze request. A world-space status label follows the brain/head limb and is the primary live display.

The public component fields on the spawned variation expose bounded timing and sensing settings. There is no fallback or observe-only runtime mode. The bundled graph is a thresholded derivative, not the full released connection graph. No persistence is attempted: saved game object/component serialization is game-version-dependent.

## Active control

The shipped runtime loads the downloaded MaleCNS FLYB payload through the allowed `ModAPI.LoadTexture("connectome/malecns-v1.0.png")` mod-asset API. The texture carrier contains the exact bytes of `malecns-v1.0.flyb.gz`; the raw payload is a repository/build input and is not copied into the deployed mod. The runtime verifies the compressed payload SHA-256, dataset identity and exact counts, and rejects missing, malformed or incompatible data. It uses a deterministic sparse leaky integrate-and-fire simulation with a capped fixed tick (default 20 Hz, at most one neural tick per physics callback), drives real sensory populations by stable MaleCNS metadata, and decodes activity from real descending/motor populations into bounded commands. The game adapter uses documented People Playground members for its primary control path:

- `PersonBehaviour.DesiredWalkingDirection` for left/right locomotion;
- `LimbBehaviour.InfluenceMotorSpeed` for differentiated head, core, arm, hand, leg, and foot motor channels;
- `GripBehaviour` for grab/drop, probed independently so its absence never disables locomotion;
- bounded restorative outputs (`BloodRegenerationPerSecond`, `RegenerationSpeed`, adrenaline and lower fire intensity) are enabled during capable active control. Regeneration boosts preserve existing rates and restore their baseline only while the last assignment is still owned by this controller.

Each discovered `LimbBehaviour` receives a deterministic actuator profile. Limb roles are inferred from component names. Side names are read from the limb hierarchy: explicit left/right names take priority, front maps to right and back to left as a game-plane convention, and unknown sides receive the average channel. This remains a heuristic; local horizontal position is not used as anatomical side. This gives every jointed human limb a brain-derived channel, but it is necessarily a fly-to-human control mapping rather than an anatomically exact human motor map.

Control does **not** add force, inject damage, spawn/delete gameplay objects, use networking/shells/native interop, or inject chemicals. The diagnostic label has its own nonphysical GameObject and is destroyed with its controller. Grip and chemistry are separate capability calls after locomotion and limb control; unsupported capability shapes are skipped, while invocation errors are not intercepted.

The overhead label reports body state, a prioritized sensory cue with its matching value, available/capable limbs, the number of joint commands applied on the last control tick, and the applied walking request. `REQUEST` shows the brain's desired limb/grip channels, not measured joint motion. `NEURAL` separates pending-input targets (`queued`), neurons processed, stale overload work dropped, and spikes (`fired`). The scheduler prioritizes fresh sensory input and drops stale recurrent work when the active set exceeds the cap, keeping control bounded instead of allowing old work to accumulate. Refractory periods expire in neural ticks even during silence. Blood loss is measured against each circulation's first valid positive blood reading because the game stores liquid amounts in native liquid units; Vitality uses its positive native value and falls back to normalized limb health when the native baseline is zero.

`PersonBehaviour.Braindead` and finite average health <= 0.001 are terminal. `BrainDamaged` alone is injury, and consciousness loss is not death. Terminal state clears neural state, immediately zeros joint speed requests using full motor influence, releases grips and restores owned regeneration boosts. Recovery starts from cleared neural state. Invalid/nonfinite health reports `INVALID DATA`/`hp=unknown` and suspends control rather than claiming death. Disabling/removing the controller also clears its commands. The label stays upright in world space without inheriting human mirroring and falls back to the root if no brain-bearing limb remains; rendering still requires an in-game check.

## Sensory and status mapping

The adapter samples Person/Limb/Circulation/Physical state and per-limb collision probes: health/damage, blood loss, pain, shock/electric charge, oxygen/suffocation, consciousness/unconsciousness, adrenaline, brain damage/seizures, balance/heartbeat, limbs/dismemberment/breakage/joint stress/paralysis/numbness/vitality, movement/rotation, contact/touch/impact/being-held, nearby entities/line direction, ambient light, active audio, fire/burn progress, temperature/hot/cold, wetness/water, blood, stab/gunshot wounds/internal bleeding, lava, weightlessness/sliding/stabbing, and zombie/infection. The router combines bounded inputs into heuristic population drives with headroom for normal contact and motion. It uses the actual R7/R8 variant types in the asset. Liquid healing and nearby direction are decoder-only inputs, not claimed neural sensory mappings. The game build does not expose a stable public smell/odor channel, so that remains unavailable rather than being invented.

The inspected interface has no native semantic vision/hearing or general disease-severity channel. Unity raycasts could provide an additional line-of-sight approximation, but this mod does not implement one. Nearby detection is a bounded allocation-free `Physics2D.OverlapCircleNonAlloc` approximation; light is Unity ambient light; audio is physical-object audio playback. Ambient temperature uses the native `AmbientTemperatureGridBehaviour` at the person location, with dynamic nearby physical objects contributing distance-attenuated heat/cold. The default ambient value of 20 is neutral; static map geometry is excluded from the object-temperature pass. Oxygen deficit is labelled `LOW OXYGEN`; `SUBMERGED HYPOXIA` also requires a submerged limb; this does not establish that water caused the oxygen loss. Collision-relative velocity is `CONTACT IMPACT`, which can include self-generated foot-floor impacts, not a claim of external vibration. `OBJECT AUDIO` requires a nearby external physical object with active, unmuted audio; it is a playback proxy, not general hearing. Known limbs are excluded even after detachment. Normal blood is excluded from exposure, Gorse blood is corrosive, and knockout poison maps to sedation. Installed liquid IDs including named poisons, reanimation/deconstruction agents, nitro, gasoline, coolant and tritium are mapped to hazard; serum identities are mapped to healing or stimulation where their names provide a safe category. Liquid identities are read from the installed build's public `CirculationBehaviour.LiquidDistribution` and `Liquid.GetIdentity` members, then mapped to bounded hazard, sedation, stimulation, healing, and water signals. Unknown identities remain exposure-only. See [API compatibility](docs/api-compatibility.md) for exact limitations.

## Build and offline verification

Requires .NET 10 SDK; no NuGet packages are needed:

```sh
dotnet format PersonConnectome.sln --verify-no-changes
dotnet build PersonConnectome.sln -c Release
dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release
dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release
git diff --check
```

The game-facing project intentionally targets `net48`. The installed game ships a classic CLR 4 / Unity Mono profile; `net48` is the project target, not a proven maximum BCL compatibility claim; its mod compiler resolves references from `People Playground_Data/Managed` as described in the [official modding guide](https://wiki.studiominus.nl/intro/boilerplate.html). `net10.0` APIs must not cross into the game-facing scripts. The game-facing project uses the installed People Playground assemblies:

```sh
dotnet build Mod/PersonConnectome.Mod.csproj -c Release
```

Set `PeoplePlaygroundInstall` when the game is installed elsewhere. The default is `C:\Program Files (x86)\Steam\steamapps\common\People Playground`.

To build and deploy the mod to that default install, run PowerShell as an administrator and use:

```powershell
.\scripts\Deploy-Mod.ps1
```

For another install, use `-GameInstall 'D:\Games\People Playground'`. Use `-NoBuild` only when the mod has already been built, or `-WhatIf` to preview the copy without changing the game directory. The script copies the manifest-listed `.cs` sources, `mod.json`, and only the PNG connectome carrier, then verifies every deployed file with SHA-256. If an older deployment contains the raw `.flyb.gz` build input, the script removes that exact stale file.

To rebuild the game-safe PNG carrier from the pinned FLYB/GZip CNS payload, run this from the repository root:

```powershell
.\scripts\Build-ConnectomeCarrier.ps1
```

Use `-InputPath`, `-OutputPath`, `-Width`, `-Height`, and optional `-ExpectedSha256` for carrier tooling. Runtime identity constants must also be deliberately updated before a different payload is accepted. The builder writes top-to-bottom PNG rows, matching the Unity runtime decoder, and refuses to finish unless the carrier round-trips to the exact input hash.

The solution also compiles the game-facing sources against the installed assemblies. `Mod/RuntimeBrain.cs` is the single brain implementation: the mod and runtime tests compile that same physical file. The runtime and adapter test projects link the actual shipped integration sources against narrow test doubles to check neural timing, payload identity, terminal cleanup, limb lifecycle, liquid/audio/oxygen semantics, regeneration ownership and invalid readings. Those doubles do not emulate Unity physics or rendering. Follow [the manual game checklist](docs/manual-game-test.md) before release.

## Layout

- `Mod/script.cs`: loadable People Playground entrypoint and runtime brain factory.
- `Mod/RuntimeBrain.cs`: single bounded MaleCNS controller implementation compiled by the mod and source/test projects.
- `Mod/ConnectomeRuntimeAsset.cs`: game-safe texture-carrier and FLYB parser.
- `Mod/PersonConnectomeController.cs`: Unity lifecycle and collision probe.
- `Mod/PeoplePlaygroundPersonAdapter.cs`: People Playground sensory and motor bridge.
- `Mod/PersonConnectomeStatusDisplay.cs`: world-space TextMeshPro state label and camera-facing display.
- `Mod/RuntimeTypes.cs`: game-facing sensory and motor value types.
- `tests/PersonConnectome.Runtime.Tests/`: source-linked runtime tests using narrow game/Unity doubles.
- `tests/PersonConnectome.Adapter.Tests/`: adapter contract tests using focused game doubles.
- `config/`: versioned settings and runtime asset identity.
- `Mod/connectome/malecns-v1.0.flyb.gz`: downloaded raw FLYB payload used by the carrier builder; it is not a deployed mod file.
- `Mod/connectome/malecns-v1.0.png`: game-facing texture carrier loaded through `ModAPI.LoadTexture`.

The asset is a thresholded derivative of the public MaleCNS v1.0 connectome: 176,422 neurons and 6,287,749 connections retained at synapse weight >= 5. See [provenance](docs/PROVENANCE.md) for the exact source, transformations, checksum, and limitations.
