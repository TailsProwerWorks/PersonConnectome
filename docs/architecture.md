# Architecture

```
optional game capabilities -> SensoryFrame -> input neurons -> LifSimulator
                                                    |             |
                                         versioned bounded state   v
game supported-action callback <- SafeMotorGate <- MotorCommand <- controller
```

`ReflectionPersonCapabilities` accesses only public instance fields/properties by name and is null-safe. It invokes no game methods and uses no People Playground type at compile time, so a changed/missing capability is represented as zero. Its explicit read-only probes cover body position/velocity/orientation, balance/contact/grounded/touch/pressure, injury/bleeding/pain/dismemberment, fire/heat/cold, shock/stun/knockout, impact/fall/acceleration, drowning/air, needs, sound/vibration, nearby direction/LOS/light, projectile/material hazard, and every listed liquid/status category. Public `Effects` entries are mapped through `EffectAliasRegistry`; unrecognised names are retained case-insensitively (64-character maximum) as local telemetry. Values are finite-clamped to `[0,1]` and vectors to `[-1,1]`. These names are candidates, not a claim about the game API.

The graph remaps arbitrary external IDs to deterministic compact indexes. Inputs and synapses are sorted; delays are bounded to 32 ticks, weights to `[-4,4]`, potentials to `[-8,8]`, and graph sizes by caller-provided maxima. The LIF update is leaky, thresholded and refractory. State JSON is version 2, refuses malformed/oversized content (64 KiB), checks vector lengths, and persists bounded delayed synapse events so restore is deterministic.

The scheduler caps catch-up at four ticks. The motor gate clamps/smooths all values, is active by default, and provides a latched emergency shutdown. The loadable game script mirrors this separation with a pure bounded `ConnectomeBrain` and a `PeoplePlaygroundPersonAdapter`; target-build game references do not enter the reusable `src/` engine.
