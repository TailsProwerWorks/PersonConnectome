# People Playground API compatibility

The active bridge is isolated in `Mod/script.cs`; `src/PersonConnectome` has no People Playground or Unity compile-time dependency. The current official internal-reference pages were inspected on 2026-09-11. This is an API mapping, not an in-game compatibility claim.

## Primary, documented active members

| API | Use | Failure behavior |
|---|---|---|
| `PersonBehaviour.DesiredWalkingDirection` | bounded left/right locomotion | attachment is disabled if `PersonBehaviour` is unavailable |
| `LimbBehaviour.InfluenceMotorSpeed(float, float)` | per-limb motor/reach posture | walking remains independent only if an individual limb fails |
| `GripBehaviour.Use(ActivationPropagation)` / `DropObject()` | optional grab/drop | reflected and individually ignored on mismatch |
| `CirculationBehaviour.BloodRegenerationPerSecond` | optional restorative output | only written with `EnableChemicalOutputs` |
| `LimbBehaviour.RegenerationSpeed` | optional healing output | only written with `EnableChemicalOutputs` |
| `PersonBehaviour.AdrenalineLevel` | optional stimulation output | only written with `EnableChemicalOutputs` |
| `PhysicalBehaviour.BurnIntensity` | optional extinguishing output | only reduced with `EnableChemicalOutputs` |

## Sensory mapping

`PersonBehaviour` supplies limbs, consciousness, shock, pain, adrenaline, oxygen, average health/speed, angle offset, and floor contact. `LimbBehaviour` supplies health/initial health, joint stress, dismemberment, zombie/infection, temperature, circulation, physical behavior and grip. `CirculationBehaviour` supplies bleeding and blood amount. `PhysicalBehaviour` supplies on-fire/burn intensity, temperature, charge, wetness, rigidbody velocity, audio playback, and last fluid identity. Unity collision probes, `Physics2D.OverlapCircleAll`, and ambient light provide bounded collision/nearby/light approximations.

Fluid identity is intentionally classified by a small case-insensitive set (`water`, `blood`, `acid`/`corros`, `poison`/`toxin`, `anaesth`/`sedat`, `healing`/`regen`, `adrenal`/`stimul`). Unknown identities are capped telemetry; they are never executed or reflected into arbitrary actions.

## Intentional limits

- The game does not expose a stable public general vision/LOS, hearing event, disease severity, drug concentration, or generic selected-person API in the inspected references. The mod therefore uses documented Unity overlap/ambient/audio approximations and an explicit spawned Human variation.
- Active motor output does not use force, direct rigidbody mutation, damage, fire creation, electric shock creation, object spawn/delete, or chemical injection.
- The script deliberately has no persistence. The current component instance is disposable, and state serialization compatibility must be established in-game before adding it.
- An offline .NET build cannot compile `Mod/script.cs`, because the game owns People Playground and Unity assemblies. The source contract test validates the expected bridge members, while the manual checklist is the definitive compatibility test.
