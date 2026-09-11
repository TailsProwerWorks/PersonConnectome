# People Playground API compatibility

The loadable bridge is in `Mod/`; the reusable `src/PersonConnectome` engine is separate. Members were checked against the installed game assemblies and official modding pages on 2026-09-11. Offline compilation and test doubles do not establish in-game physics behavior.

## Runtime target boundary

The game-facing project targets `net48` and references installed People Playground/Unity assemblies. Installed `Assembly-CSharp` reports CLR `v4.0.30319`; the shipped Mono BCL files report `4.6.57.0`, with a netstandard 2.0 facade. This does not prove that every .NET Framework 4.8 API is available or that net48 is the highest compatible target. The current code uses members present in the inspected assemblies. Installed Roslyn 5 supports the modern C# syntax. The separate engine and test runners target `net10.0`, with no project reference from the mod to them.

## Public active members

| API | Use and boundary |
|---|---|
| `PersonBehaviour.DesiredWalkingDirection` | Bounded walking request, cleared on stop/disable. |
| `LimbBehaviour.InfluenceMotorSpeed(float, float)` | The game interpolates joint speed toward the request. Stop uses influence 1, independent of `IsCapable`; no-joint limbs cannot be driven by this API. |
| `GripBehaviour.Use(ActivationPropagation)` / `DropObject()` | Optional grab/drop. Installed `Use` does not read its propagation argument. Stop releases grips independently of capability. |
| `CirculationBehaviour.BloodRegenerationPerSecond` / `LimbBehaviour.RegenerationSpeed` | Bounded boosts never lower an existing baseline. Cleanup restores only an unchanged last assignment owned by this controller. A newer external write wins. |
| `PersonBehaviour.AdrenalineLevel` | Bounded stimulation/calming during active control. |
| `PhysicalBehaviour.BurnIntensity` | Bounded reduction during active control. |

Restorative outputs are engineered interventions. They do not demonstrate human physiology or a biological fly-to-human chemistry mapping.

## Limb discovery and routing

Each read reconciles child limbs (including inactive children) and the public `PersonBehaviour.Limbs` array. Previously discovered detached limbs remain tracked; destroyed Unity objects are removed. Each surviving limb has one controller and collision probe. Names classify head, core, arm, hand, leg and foot roles. Side names are resolved through the hierarchy: explicit left/right takes priority; front->right and back->left is a documented game-plane convention; unknown naming receives an average channel. Position is not interpreted as anatomical side. Exact prefab mapping still requires the manual checklist.

Missing grips do not disable joint or walking control. Incapable limbs have old motor commands and grips cleared. Terminal, invalid-health, consciousness <= 0.8 and freeze states suppress walking, limb/grip requests and active chemistry. Only terminal/invalid reads stop neural stepping; nonterminal unconsciousness is still sampled. Recovery from terminal state begins with cleared neural state.

## Sensory mapping

| Source | Direct members sampled | Interpretation |
|---|---|---|
| `PersonBehaviour` | Health, pain, shock, consciousness, oxygen, adrenaline, fire, wetness, speed, angle, balance, heartbeat, brain damage/time, brain death, seizure time, floor contact | Bounded scalars and flags. Nonfinite average health is invalid data, not invented death. |
| `LimbBehaviour` | Health/initial health, joint stress, dismemberment, breakage/shattering, temperatures, frozen/paralysed/capable/numb/vitality/lung/zombie state | Injury/temperature/state aggregations are approximations. |
| `CirculationBehaviour` | Bleeding, blood amount, heart rate, flow/disconnection/circulation, wound counts, internal bleeding, `LiquidDistribution` | Blood deficit, wound severity and fluid fractions are bounded approximations. |
| `Liquid` | `GetIdentity` and display-name fallback; `RefFloat.Raw` amounts | Normal `BLOOD`/`Human blood` excluded from exposure; Gorse blood is corrosive. Knockout/anesthetic/anaesthetic/sedation map to sedation. Other poison/acid/toxin/zombie identities map to hazard; stimulation/healing/water have separate categories. Unknown liquid effects are not invented. |
| `PhysicalBehaviour` | Burn state/progress, temperature, charge, wetness, underwater/lava/stab/contact/held/weightless/sliding/disintegration and audio source | Underwater is separate from wetness. Submerged hypoxia reports simultaneous submersion and oxygen deficit, not proof that water caused the deficit; hypoxia alone is low oxygen. |
| Collision callbacks | Relative contact velocity outside the owner hierarchy | `CONTACT IMPACT`, including self-generated foot-floor impacts. This is not externally caused vibration or sound. |
| Unity overlap/closest point | Nearby physical colliders and horizontal surface direction | Bounded proximity; no occlusion/semantic vision. Fixed 128-hit buffer can omit objects in crowded scenes. Known own limbs are excluded after detachment too. |
| Ambient light/audio | Global ambient grayscale; active unmuted nearby physical-object audio volume | Light/playback proxies, not visual perception or general hearing. |

The neural router weights tonic contact/heartbeat so ordinary standing retains input headroom. R7/R8 variant names match the bundled asset. Liquid healing and nearby direction remain decoder-only inputs; no validated neural population mapping is claimed for them. Smell/odor and general semantic vision/hearing are not exposed as native senses here. Physics raycasts could support an additional line-of-sight approximation, but one is not implemented.

## Display and safety

The primary TextMeshPro label is a world-space object owned by the controller, independent of limb mirroring/rotation/destruction. It follows the current brain-bearing limb or root. `REQUEST` is desired motor output; `LIMBS` reports capable/applied joint counts and the applied walking request, not achieved physical motion. `NEURAL` distinguishes pending-input targets, processed/deferred neurons and fired neurons. The sensory label and its value share a single selection path.

Mod sources use memory streams, gzip and a pure byte/word SHA-256 implementation on texture bytes. The game rejects `using System.Security` and its subnamespaces, so the runtime checksum has no platform cryptography dependency; source guards cover forbidden namespace prefixes and aliases. Offline tests compare the portable checksum against known vectors, padding boundaries and the framework SHA-256 implementation. They do not use filesystem APIs, reflection, networking, process execution, native interop, direct rigidbody forces, damage injection or liquid injection. The only explicit GameObject creation/deletion is the nonphysical diagnostic label. No game persistence is implemented.

The solution compiles the mod against installed references. Two source-linked test harnesses check neural and adapter contracts with test doubles; the manual game checklist remains required for native loading, physics, rendering and performance acceptance.
