# People Playground API compatibility

The loadable bridge and authoritative runtime are in `Mod/`. Members were checked against the installed game assemblies and official modding pages on 2026-09-11. Source-linked test doubles do not establish in-game physics behavior.

## Runtime target boundary

The game-facing project targets `net48` and references installed People Playground/Unity assemblies. Installed `Assembly-CSharp` reports CLR `v4.0.30319`; the shipped Mono BCL files report `4.6.57.0`, with a netstandard 2.0 facade. This does not prove that every .NET Framework 4.8 API is available or that net48 is the highest compatible target. The current code uses members present in the inspected assemblies. Installed Roslyn 5 supports the modern C# syntax. The game project and runtime test harness compile the same physical `Mod/RuntimeBrain.cs`; no separate offline brain library is loaded by the game.

## Public active members

| API | Use and boundary |
|---|---|
| `PersonBehaviour.DesiredWalkingDirection` | Bounded walking request, cleared on stop/disable. |
| `LimbBehaviour.InfluenceMotorSpeed(float, float)` | The game interpolates joint speed toward the request. Stop uses influence 1, independent of `IsCapable`; no-joint limbs cannot be driven by this API. |
| `GripBehaviour.Use(ActivationPropagation)` / `DropObject()` | Optional grab/drop. Installed `Use` does not read its propagation argument. Stop releases grips independently of capability. |
| `CirculationBehaviour.BloodRegenerationPerSecond` / `LimbBehaviour.RegenerationSpeed` | Bounded boosts never lower an existing baseline. Cleanup restores only an unchanged last assignment owned by this controller. A newer external write wins. |
| `PersonBehaviour.AdrenalineLevel` | Bounded stimulation/calming during active control. |
| `PhysicalBehaviour.BurnIntensity` | Bounded reduction during active control. |
| `ContextMenuOptionComponent.Buttons` / `ContextMenuButton.Identity` | Controlled people suppress only the native walking/protective/sitting/resting/stumbling pose actions; unrelated context-menu actions remain available and the original buttons are restored when the controller is disabled. |

Restorative outputs are engineered interventions. They do not demonstrate human physiology or a biological fly-to-human chemistry mapping.

## Limb discovery and routing

Each read reconciles child limbs (including inactive children) and the public `PersonBehaviour.Limbs` array. Previously discovered detached limbs remain tracked; destroyed Unity objects are removed. Each surviving limb has one controller and collision probe. Names classify head, core, arm, hand, leg and foot roles. Side names are resolved through the hierarchy: explicit left/right takes priority; front->right and back->left is a documented game-plane convention; unknown naming receives an average channel. Position is not interpreted as anatomical side. Exact prefab mapping still requires the manual checklist.

Missing grips do not disable joint or walking control. Incapable limbs have old motor commands and grips cleared. Terminal, invalid-health, consciousness <= 0.8 and freeze states suppress walking, limb/grip requests and active chemistry. Only terminal/invalid reads stop neural stepping; nonterminal unconsciousness is still sampled. Recovery from terminal state begins with cleared neural state.

## Sensory mapping

| Source | Direct members sampled | Interpretation |
|---|---|---|
| `PersonBehaviour` | Health, pain, shock, consciousness, oxygen, adrenaline, fire, wetness, speed, angle, balance, heartbeat, brain damage/time, brain death, seizure time, floor contact | Bounded scalars and flags. Nonfinite average health is invalid data, not invented death. Falling combines downward velocity from tracked limb physical bodies with floor-contact gating. |
| `LimbBehaviour` | Health/initial health, joint stress, dismemberment, breakage/shattering, temperatures, frozen/paralysed/capable/numb/vitality/lung/zombie state | Injury/temperature/state aggregations are approximations. `LimbLoss` is the fraction of tracked limb slots marked dismembered/disintegrated, while `LIMBS lost=x/y` shows the underlying count. |
| `CirculationBehaviour` | Bleeding, blood amount, heart rate, flow/disconnection/circulation, wound counts, internal bleeding, `LiquidDistribution` | Blood deficit, wound severity and fluid fractions are bounded approximations. |
| `Liquid` | `GetIdentity` and display-name fallback; `RefFloat.Raw` amounts | Normal `BLOOD`/`Human blood` excluded from exposure. Poison, acid, gorse, toxin, zombie/reanimation, deconstruction, combustion, osteomorphosis, nitro, gasoline, coolant, oil and tritium identities map to hazard; knockout/anesthetic/anaesthetic/sedation map to sedation; adrenaline/stimulation/enhancing/ultra-strength/durability map to stimulation; regeneration/healing/immortality/life/mending/coagulation map to healing; water has a separate exposure category. Unknown liquid effects are not invented. |
| `PhysicalBehaviour` | Burn state/progress, temperature, charge, wetness, underwater/lava/stab/contact/held/weightless/sliding/disintegration, native `rigidbody` and audio source | Underwater is separate from wetness. Submerged hypoxia reports simultaneous submersion and oxygen deficit, not proof that water caused the deficit; hypoxia alone is low oxygen. Projectile awareness requires a native projectile component; `BulletPenetration` alone is not treated as projectile evidence. Falling reads the native limb rigidbody velocity, not unrelated child bodies or horizontal walking speed. |
| Collision callbacks | Relative contact velocity outside the owner hierarchy | `CONTACT IMPACT` plus a decaying `Vibration` channel. This is mechanical contact, including self-generated foot-floor impacts, not external sound. A projectile callback is raised only for an external collider carrying a native projectile component. |
| Unity overlap/closest point/linecast and ambient grid | Nearby physical colliders, horizontal surface direction, the nearest line-of-sight target, and native local ambient temperature | `Nearby` is bounded proximity. `Vision` is only a bounded visual proxy: the nearest external collider must be hit by a linecast and is attenuated by ambient light; it has no color, identity, depth or semantic recognition. `AmbientTemperatureGridBehaviour.GetTemperatureAtPoint` supplies local ambient heat/cold; dynamic nearby `PhysicalBehaviour.Temperature` values and native `LavaBehaviour.LavaTemperature` add distance-attenuated object cues, while static map geometry is excluded. The default ambient value of 20 is neutral. This is not native long-range radiation. Fixed 128-hit buffer can omit objects in crowded scenes. Known own limbs are excluded after detachment too. |
| Ambient light/audio | Global ambient grayscale; active unmuted nearby physical-object audio volume | Light/playback proxies, not visual perception or general hearing. Generic nearby `Root`/`Root` audio is treated as the game's generated self-audio and ignored. |

The neural router weights tonic contact/heartbeat so ordinary standing retains input headroom. Blood loss is normalized against a per-circulation positive baseline because the game stores liquid quantities in native liquid units; zero/unavailable initial readings are treated as unknown. Vitality uses the native positive value and falls back to normalized limb health for a zero baseline. Proprioception combines bounded body motion, rotation, balance and joint-stress proxies; it is not direct joint-angle reconstruction. Nearby direction remains decoder-only geometry and does not by itself force escape walking. R7/R8 variant names match the bundled asset. Liquid healing remains decoder-only; no validated neural population mapping is claimed for these fields. Smell/odor and general semantic vision/hearing are not exposed as native senses here; `Vision` is only the line-of-sight/light proxy documented above, and target labels are conservative game-object classifications rather than learned semantics.

## Display and safety

The primary TextMeshPro label is a world-space object owned by the controller, independent of limb mirroring/rotation/destruction. It follows the current brain-bearing limb or root. `REQUEST` is desired motor output; `LIMBS` reports driveable/applied joint counts and the applied walking request, not achieved physical motion. Nonterminal motor eligibility uses local joint, fracture, dismemberment, disintegration, paralysis and circulation-disconnection evidence rather than the game's person-wide `IsCapable` flag; this prevents submersion from disabling every healthy limb. In a terminal state, the display reports one explicit stopped-control reason instead of labeling every limb independently incapable. `NEURAL` distinguishes pending-input targets, active work, processed/dropped neurons, fired neurons and the real-time-bounded scheduler. The sensory label and its value share a single selection path.

Mod sources use memory streams, gzip and a pure byte/word SHA-256 implementation on texture bytes. The game rejects `using System.Security` and its subnamespaces, so the runtime checksum has no platform cryptography dependency; source guards cover forbidden namespace prefixes and aliases. Offline tests compare the portable checksum against known vectors, padding boundaries and the framework SHA-256 implementation. They do not use filesystem APIs, reflection, networking, process execution, native interop, direct rigidbody forces, damage injection or liquid injection. The only explicit GameObject creation/deletion is the nonphysical diagnostic label. No game persistence is implemented.

The solution compiles the mod against installed references. Two source-linked test harnesses check neural and adapter contracts with test doubles; the manual game checklist remains required for native loading, physics, rendering and performance acceptance.
