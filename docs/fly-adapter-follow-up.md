# Fly integration and reference research

The fly variation is enabled and reuses `Core/LifBrain.cs`, the bundled MaleCNS derivative and `FlyMotorCommand`. There is no second brain, random motor fallback, added neural edge, imported controller or new dependency.

## Reference findings

Inspected on 2026-09-19:

- [fly-connectome-template](https://github.com/cobanov/fly-connectome-template), [FlyScene.tsx](https://github.com/cobanov/fly-connectome-template/blob/main/src/components/FlyScene.tsx) and [model integration](https://github.com/cobanov/fly-connectome-template/blob/main/docs/MODEL-INTEGRATION.md) supply anatomy and a model-output viewer. The Flybody surface mesh is not a physics controller. Its custom attribution license applies to reuse; this change copies no code or assets.
- [NeuroCraft Fly](https://github.com/evnsnclr/neurocraft-fly-public) describes game inputs, modeled neural activity, labeled readouts and scripted body programs. The public repository has no runnable mod or companion source yet.
- [DOOM-x-Fly](https://github.com/Aur1ety/DOOM-x-Fly), its [rate-network core](https://github.com/Aur1ety/DOOM-x-Fly/blob/main/flybrain/model/core.py) and [trained readout](https://github.com/Aur1ety/DOOM-x-Fly/blob/main/flybrain/train/level_bc.py) use a sparse connectome substrate with a separately trained action decoder. This mod retains its own named-population heuristic decoder; it does not inherit DOOM's trained policy or results.
- [FlyProject](https://flyproject.io/) serves inline JavaScript: `sandboxTick` senses stimuli; `EscapeNet.setLoomDrive` and `step` simulate a small escape circuit; TTMn spikes trigger `flyJump` with a cooldown. `stepFly` separately handles locomotion, gravity, collisions and animation. Other sandbox responses use explicit stimulus-to-body rules. Its selected circuits and calibrated game gains differ from simulating this mod's whole retained graph.

The common architecture is **game readings → neural model → declared readouts → body actuator**. Connectivity alone is not a complete animal controller. LIF parameters, sensory encoding, population decoding and physics gains remain engineering choices, not recovered fly physiology.

## Sensing

`src/Adapters/Fly/FlyEnvironmentSensor.cs` scans up to 32 colliders within 12 world units, excluding self, disabled, inactive and trigger colliders. Five head-relative frontal sectors retain their nearest candidate, each with at most one 16-hit linecast. Full buffers and blocked candidates mark vision as partial. The search can miss farther visible objects in the same sector.

The head is local -X, including rotation and mirroring. Ambient brightness gates visual strength. Collider extent and relative velocity produce angles in degrees and expansion/motion in degrees per second, matching the neural encoder. These are collider proxies, not a rendered retina. Static geometry can loom when the fly approaches it.

Contact, temperature, fire, wetness, charge and disintegration come from native physics state. Nearby objects supply distance-weighted heat/cold and active `MainAudioSource` playback. This pass does not inspect arbitrary child sound sources, audio spectra or the Human adapter's local-lamp footprints.

Stock Pumpkin identity supplies modeled proximity and mouth-area contact cues. Contact requires the native head collider touching the Pumpkin and its nearest surface lying within 0.18 scaled units of the mouth. `Fly Treat [ShadowNineX]` is a separate marked food item; only its mouth contact produces a bounded positive dopamine-style event. Proximity includes walls. There is no odor transport, taste measurement, nutrition, healing, or consumption.

`src/Adapters/Fly/FlyHealth.cs` reports bounded overall and regional head, thorax, abdomen, left/right-wing and individual-leg intactness. A new impact can emit a one-sample damage event, apply sparse negative dopamine-style reinforcement, and record the other object's spawnable category in `src/Adapters/Fly/FlyExperienceMemory.cs`. The memory is per fly, bounded, and category-level rather than a saved scene history. When a remembered category re-enters the sampled visual field, its learned-threat strength is added to the normal visual/threat input path; it does not bypass the graph with a direct motor command.

## Movement and model

Walking follows the visible head/mirror direction. The decoder still reports MDN as the distinct `backward` channel. On the articulated fly, a strong backward request (0.3) reflects the complete centered Person root once, then drives the ordinary forward tripod in the new facing until the request releases below 0.12. This is a debounced People Playground body program chosen to avoid an unstable mirrored stride; it is not a claim that MDN biologically makes a fly turn around. Halt/brake preempt turning, drive and flight support. Angular speed is bounded. Wing motor activity is sufficient to request autonomous flight and retains support for its short hold; landing releases support. Jump is a rising-edge contact-gated impulse with a 0.75-second cooldown. Escape remains the core's qualified neural output and retains backward/forward intent; with neither, the actuator defaults to retreat from the frontal field. Held flight keeps wings animated. Learned threat can therefore influence ordinary visual/threat populations and a later escape request, but it is not a scripted category-to-retreat rule.

The body pose no longer sums weak grooming populations. Grooming requires one named grooming channel to reach 0.35 while the fly is idle, grounded locomotion and flight are quiet, and escape is inactive. The threshold and visible foreleg pose are actuator settings. The original antenna/head/leg/abdomen readouts remain separate in telemetry; no reference project establishes this pose as reconstructed biological motor output.

Visible-object following is now a declared, bounded readout at the adapter boundary. When a lit, line-of-sight target has valid geometry and is not looming, its head-relative bearing supplies at most 0.65 yaw and its visual strength supplies at most 0.55 forward intent. The assist is rate limited, decays when the target disappears, and is suppressed by halt, reverse, projectile/looming cues and escape. This follows the reference projects' separation between recurrent neural activity and a small action decoder; it is not presented as recovered fly physiology or object semantics.

Physics actuation runs every fixed update using the latest neural command. Neural sampling defaults to 20 Hz. Disable clears active control without erasing passive falling velocity; re-enable clears held commands and resumes. Static/frozen bodies and invalid values cannot be actively driven.

The fly now has 17 separate native physical limbs, with breakable joints, per-part skin/circulation, and a two-link IK tripod gait. Walking uses leg contact and joint torque; flight remains an engineered actuator requiring two usable wings. Head, body and appendages can bleed and dismember through native PPG components. See [body design](fly-body-design.md).

## Reinforcement and limits

The shared Training controls apply explicit Good/Bad reinforcement to either body type using recently eligible, pre-existing connectome edges. The fly runs frozen-baseline inference until an explicit Teach trial is started; during that trial, positive Treat-contact and negative damage events use the same bounded eligibility/reward mechanism, not a custom action policy. Ending the trial returns to frozen baseline or learned read-only playback. The profile is deliberately small and only changes edge efficacy deltas; it does not add neurons, labels, recognition code, or a second brain.

This supports a narrow gameplay association such as “this previously damaging spawnable category now contributes a visual threat signal.” It cannot comprehend a gun, infer who threw an object, identify a bowling ball by meaning, reason about causes, or make the simulation crow-smart. Visual category names and collider geometry remain game proxies, not semantic perception.

## Evidence and limits

Regression coverage includes mirroring, reverse, bounded movement, halt, wing-request flight/landing, jump cooldown, invalid values, frozen bodies, resume, degree-based vision, occlusion, Pumpkin/Treat identity and contact, regional health, learned-category threat, reinforcement, and disintegration.

A matched 80-tick real-payload probe introduces an approaching-object cue on ticks 20–59. It changes spike counts on 58 ticks and motor readouts on 56 ticks compared with the same light-only history. That demonstrates propagation into motor output; it does not establish correct escape direction, biological fidelity or superiority over shuffled wiring.

Native loading, rendering, collisions, performance and interaction feel still require the [fly checklist](manual-game-test.md#fly-variation). Compiler acceptance and doubles are not native gameplay evidence.
