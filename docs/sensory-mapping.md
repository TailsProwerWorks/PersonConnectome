# Sensory and movement mapping

This is an independently implemented C# adaptation of population choices in the Minecraft project's [SensoryEncoders](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/SensoryEncoders.java), [MotorMap](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/MotorMap.java) and [MotorDecoder](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/MotorDecoder.java). It uses our existing authoritative LifBrain and unchanged pinned MaleCNS asset. The reference's [research notes](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/docs/REFERENCE.md) explain its choices. No Minecraft dependency is required.

## What actually enters the graph

Counts below are resolved from the bundled asset, not invented neuron groups. `input:` names are local indexes over its existing annotations. Except photoreceptors, input groups require sensory superclass membership (`cb_sensory`, `vnc_sensory`, `sensory_ascending` or `sensory_descending`). Feature-level visual inputs target named interneurons and are explicitly engineered encoders.

| Observed game signal | Asset selection | Members | Interpretation and limits |
|---|---|---:|---|
| Ambient + bounded local-light estimate | `ol_sensory` + class `visual` | 6,091 | Uniform photoreceptor amplitude from ambient plus strongest supported nearby light footprint; no color channels, UV or spatial retina. |
| Increase/decrease between valid combined-light samples | `Mi1` / `L2`, `L3` | 1,773 / 1,779 / 1,772 | ON/OFF entry choices adapted from the reference. Single scalar light-level change, including lamp movement/toggling; first sample, invalid data and terminal recovery establish a new baseline without a fabricated transition. No dark tonic current or per-column image is added. |
| Strongest accepted external playback | class `mechanosensory`, subclass `auditory` | 114 | Volume/distance proxy with world-horizontal L/R weighting. Valid source spectra split energy below/above 100 Hz; drive is max(high, low * 0.7). Unavailable spectra use broad playback. No sound meaning or song recognition. |
| Head contact | sensory superclass, type prefix `BM_` | 863 | Human head to fly head-bristle analogy. |
| Arm contact | tactile class, nerve `ProLN` | 266 | Human arms to fly foreleg tactile analogy. |
| Leg contact | tactile class, nerve `MesoLN`/`MetaLN` | 1,611 | Human legs to fly middle/hindleg tactile analogy. |
| Core contact | tactile class, nerve `PDMN`/`DMetaN`/`AbN3`/`AbN4` | 303 | Human torso to fly posterior/abdominal tactile analogy. |
| Broad impact, vibration and new injury event | head above plus all tactile class | 3,421 | Broad proxy, not a localized pain receptor. Remaining 378 tactile cells are an other group. Disjoint regions merge broad/local drive by maximum; they are never injected twice by this router. Foot-floor contact can be self-generated. |
| Native signed body tilt | class `mechanosensory`, subclass `wind_gravity` | 475 | Engineered body-to-antenna gravity proxy, not measured wind. |
| Connected hinge angle magnitude | proprioceptive class, subclass `hair plate` | 113 | Peak valid angle/180, not a reconstructed fly joint. |
| Connected hinge speed magnitude | proprioceptive class, subclass `chordotonal organ` | 425 | Peak valid speed/180, not a calibrated chordotonal tuning curve. |
| Connected hinge stress | proprioceptive class, subclass `campaniform sensilla` | 426 | Peak valid stress/100. Invalid or disconnected joints cannot contribute through another valid limb. Whole-body injury stress remains separate telemetry. |
| Native body/local/proximity-derived heat | thermosensory class, `TRN_VP2` | 7 | Warm channel; game thresholds are engineering scales. |
| Native body/local/proximity-derived cold | thermosensory class, `TRN_VP3a` or `TRN_VP3b` | 7 | Cool channel; no claim of adult nociceptive calibration. |
| Visible expanding collider bounds | `LC4`, `LPLC2` | 126 / 185 | Approximate angular expansion and size tuning; positive relative closing-speed proxy remains a fallback without valid bounds geometry. Mere proximity/recession does not drive looming. |
| Small moving visible collider | `LC11`, `LC18` | 143 / 208 | Bounds size <15 degrees and sweep >5 degrees/s. This is a geometric feature, not object recognition. |
| Native body rotation under light | `visual_projection` superclass, type prefix `VS` | 34 | Engineered optic-roll proxy from native angular speed, not measured retinal flow or 3D head rotation. |

Thermal choices are supported by [adult thermo/hygrosensory connectomics](https://pmc.ncbi.nlm.nih.gov/articles/PMC7443704/). In particular, a `TRN` prefix alone is insufficient: VP1m participates in hygrosensation and is not included as a warm/cool receptor. Auditory and gravity modalities are supported by [Johnston's-organ auditory analysis](https://pmc.ncbi.nlm.nih.gov/articles/PMC4023023/) and [wind/gravity sensory analysis](https://pmc.ncbi.nlm.nih.gov/articles/PMC2755041/); these sources do not validate a human game body's input scaling.

All eligible population members can receive input, replacing a first-N cutoff that favored low IDs. Refractory neurons still cannot integrate input. Audio direction is delta.x/distance in world coordinates. Visual encoders use head-relative bearing/90: counterclockwise in the 2D plane reduces L drive and clockwise reduces R drive. This is an engineering projection, not anatomical fly-eye laterality. Missing direction uses equal bilateral drive without claiming localization; unknown-side annotations are not assigned a side. Amplitudes are clamped, finite normalized values, not firing rates. The Brain page reports requested encoder amplitudes and unique input neurons queued this tick; recurrent spikes can occur elsewhere too.

## Injury, contact and visual feature scales

`DamageEvent` is the largest normalized health decrease across connected readable limbs since their previous valid sample, not the total of all wounds. First samples, InitialHealth changes, invalid/disconnected/lost readings, suspension and terminal state clear/re-prime history. A fatal limb transition can emit once while the person remains nonterminal. Sampling can miss injury/healing between reads or damage immediately followed by disconnection.

The reference accumulates accepted Minecraft damage and merges it into tactile drive. Our equivalent uses actual sampled limb health, with `q = (DamageEvent / 0.2)^1.5`, encoder amplitude `min(1, 2*q/(1+q))`. Hill exponent 1.5 and scale 0.2 follow its encoder shape; gain 2 is local to our discrete threshold-1 model. A 20% event reaches threshold, and larger events saturate; smaller events contribute graded potential without guaranteeing a spike. This avoids a lone moderate injury event merely decaying forever below threshold in a quiet graph. It does not measure felt pain, calibrated nociception or adrenaline. Native `Pain` remains a separate game reading.

Known regional floor/contact/held flags contribute amplitude 0.15. Broad impact plus vibration*0.35 is clamped; when regional localization is unavailable, sustained broad touch/contact contributes 0.15 per channel. Broad contact/injury and local input combine by maximum. Human-to-fly region names are engineering analogies, not anatomical homology. Water alone is not contact; whole-body impact/vibration can still stimulate several regions.

Each of five 36-degree head-relative view bands uses its nearest visible collider's radius `r = max(bounds.extents.x, bounds.extents.y)`, center distance `d`, and target-minus-anchor native velocity. Size is `2*atan(r/d)`; expansion is positive `2*r*closing/(d*d+r*r)`; sweep is `abs(cross(delta, relativeVelocity)/(d*d)*180/pi - headAngularVelocity)` in degrees/second. This approximates rigid 2D bounds, omitting target deformation/rotation, rendered silhouettes and camera retinal flow. Finite positive geometry, velocities and head angular velocity are required. Visibility is restricted to the actual connected head's frontal 180-degree field and the nearest 16 candidates are checked in distance order. Per-band head bearing affects the existing visual population-side weights. Each feature takes the maximum L and R contributions across bands rather than summing duplicate drives; no new direct visual-to-motor bypass is added. See [head-relative vision](head-relative-vision.md).

Following the reference, LC4 amplitude is `expansion/(expansion+200)`, LPLC2 is Gaussian size tuning centered at 60 degrees with sigma 25 while expanding, and LC11/LC18 use `sweep/(sweep+100)` for small moving targets. All are multiplied by existing visibility. The VS proxy uses `abs(nativeAngularSpeed)/(abs(nativeAngularSpeed)+300) * light`; it is unsigned and gated by valid light/rotation. These are deterministic feature encoders, not biological-rate measurements.

Looming enters LC4/LPLC2; an actual DNp01 spike is still required for a neural escape request. Unlike the raw reference-style readout, the Human decoder now also requires recent threat evidence to avoid labeling unrelated recurrent activity as escape. `DNp01-fired` always reports raw firing, while `escape-request` reports the accepted request.

## Deliberate unmapped readings

Apart from the new sampled health-drop event above, health, pain, shock, oxygen, consciousness, blood, wounds, brain injury, zombie state and internal liquid identities remain native/derived telemetry and existing control constraints or restorative adjustments. They are not injected into photoreceptors, descending walking neurons, taste or smell populations. Their separately measured mechanical or thermal consequences can still reach the corresponding encoders. A circulating syringe chemical does not establish a fly's external taste stimulus. Wetness/submersion is not ambient humidity; no unsupported hygro channel is synthesized. Velocity XY is telemetry, not wind. No semantic object vision, measured smell/taste, feeding, flight, courtship or biologically established human grasping is implemented. Explicit stock-Pumpkin gameplay cues are described below; they never derive from internal blood chemistry.

The injury-event route is explicitly a mechanical proxy adapted from the reference. It does not restore the older unrelated visual/descending hazard/liquid stimulation. The mod senses more state than it has defensible neural encoders for. An unmapped reading is still displayed; it is not proof that the brain understands that state.

## Movement readout

The reference walking weights are DNp09 0.30, DNg100 0.25, DNge053 0.15, DNge050 0.15 and DNg97 0.15. Our input to the decoder is fired/member fraction, not the reference's Hz readout. Forward, backward and side-specific leg channels use exponential smoothing with a 150 ms time constant. Turning combines DNa02 R-L + 0.5*DNg13 R-L + 0.25*DNa01 R-L with a 100 ms filter. It supplies head/core joint proxies in this 2D body, not facing rotation or flight steering.

Forward mode enters at filtered 0.08 and exits below 0.04, with a 250 ms minimum dwell. Raw MDN fraction >=0.5 immediately selects backward over forward; backward exits below filtered 0.25 after its dwell. Active modes use an absolute 0.3 walk-request floor before the existing rate limiter. At default adapter gain 2, a settled 0.3 submits 0.6 and can clear the native walking gate of 0.5. This is an actuator conversion, not an increase in reported neural activity. Mode and filtered activity are displayed separately. Short quiet gaps are bridged, but sustained silence stops walking.

Raw DNg60/DNg74_a/DNg74_b or AN19A018 fraction >=0.2 clears movement immediately, as do unsafe/terminal control states. These thresholds and the floor are local engineering choices. Fly leg motor subclasses `fl`, `ml`, `hl` contribute side-specific joint proxies (381 total); wing/song/proboscis populations are excluded. MN9 does not request human grips.

The normal request rate limit is eight normalized units per elapsed game second, with elapsed time capped at 0.25 seconds. There is no added oscillator, sensor-only escape direction or force application. Actual walking depends on native pose selection, torque, grounding and sustained requests. Native standing mechanics and existing sensory-derived restorative adjustments remain; exclusive neural control of every game action is not claimed.

Threat telemetry keeps the stages visible: `body` is normalized pain/fire/shock/submerged-hypoxia/projectile safety input; `looming` is effective LC4/LPLC2 input after manual overrides; `DNp01-fired` is actual neural firing. An escape request additionally requires movement permission and evidence within 0.5 game seconds (body >0.5, sampled DamageEvent >0.001, or effective looming >0.05). The sampled injury event is shown to four decimal places so qualifying small health changes do not round to zero. The remaining evidence window is displayed separately; temporal coincidence does not prove that evidence caused the spike. No injury is fabricated, and pre-impact looming remains eligible. Ordinary light, audio, floor contact and recurrent activity alone no longer qualify. Native hazard avoidance remains independent, and the existing 1.5-second event cooldown/0.25-second quiet rearm remain. Long callback gaps expire context using actual elapsed game time; invalid/terminal/unconscious states clear it. These are local gameplay thresholds, not validated biological escape criteria.

## Measured connection checks, not biological validation

Refreshed on 2026-09-14 after the scheduler propagation-budget change and rechecked after the head-relative vision update (identical counts). The two directional approach cases now supply head bearings of -90/+90 degrees to test the full population-side weighting range; native view selection excludes its exact perpendicular boundary. Reproduce with `dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release -- --mapping-report`. The added spatial case uses two 45-degree bearings, strength 1, 60-degree size and 200 deg/s expansion. Each case starts a fresh real graph and runs 40 measured ticks at 0.05 game seconds with the scenario-specific synthetic frame (normally amplitude 1). Injury cases submit one pulse, then zero; geometry/rotation cases use explicit units. ON/OFF cases first prime the preceding valid light sample. These are synthetic sensor frames against the shipped graph, not live game trials.

| Case | Input-population spikes | Whole-graph spikes | Dropped spikes | Peak absolute walk request |
|---|---:|---:|---:|---:|
| Quiet | 0 | 0 | 0 | 0.000 |
| Constant light | 42,637 | 42,637 | 0 | 0.000 |
| Light increase (Mi1) | 1,773 | 195,215 | 0 | 0.300 |
| Light decrease (L2 counted) | 961 | 139,998 | 0 | 0.300 |
| Left audio | 424 | 145,677 | 0 | 0.300 |
| Right audio | 363 | 109,435 | 0 | 0.300 |
| Impact (tactile counted) | 17,906 | 205,577 | 0 | 0.300 |
| 50% injury pulse (head counted) | 863 | 129,862 | 0 | 0.300 |
| 100% injury pulse (head counted) | 863 | 129,862 | 0 | 0.300 |
| Head contact | 2,296 | 89,765 | 0 | 0.300 |
| Arm contact | 720 | 89,860 | 0 | 0.300 |
| Leg contact | 3,424 | 95,453 | 0 | 0.300 |
| Core contact | 402 | 97,428 | 0 | 0.300 |
| Loom (LPLC2 counted) | 1,295 | 141,902 | 0 | 0.300 |
| Small moving (LC11 counted) | 286 | 126,574 | 0 | 0.300 |
| Roll | 134 | 154,162 | 0 | 0.300 |
| Low-band audio | 674 | 133,304 | 0 | 0.300 |
| High-band audio | 784 | 145,089 | 0 | 0.300 |
| Warm | 49 | 145,539 | 0 | 0.300 |
| Cool | 49 | 132,540 | 0 | 0.300 |
| Joint motion | 2,975 | 153,853 | 0 | 0.300 |
| Left tilt | 1,591 | 157,731 | 0 | 0.300 |
| Right tilt | 1,726 | 143,356 | 0 | 0.300 |
| Left approach fallback (LC4 counted) | 483 | 133,844 | 0 | 0.300 |
| Right approach fallback (LC4 counted) | 362 | 131,572 | 0 | 0.300 |
| Two spatial approach bands (LC4 counted) | 329 | 141,902 | 0 | 0.300 |

Totals include repeated spikes/work across ticks, not unique neurons. Left/right populations differ in membership and wiring; equal raw counts are not expected. The input counts can include recurrent firing of those same neurons. Constant photoreceptor drive alone produced no downstream spikes in this silent-start test; the photoreceptors have outgoing edges, so that is not evidence of disconnected data. Inhibitory input does not initiate excitation in a silent network. The reference supplies additional lamina tonic and spatial visual processing that this runtime does not reproduce.

These cases all completed without truncation after separating full input integration from the 24,000-spike propagation budget. Larger/custom bursts can still drop threshold crossings. The new dropped-spikes count is not the old unintegrated-neuron count; their units differ. Bounded memory/spikes are not a guaranteed CPU frame time. See [scheduler measurements](scheduler-performance.md). The model still uses discrete decay, five intervening refractory ticks and one-tick transmission delay at a default 20 Hz control rate. It does not implement the reference's 0.5 ms integrator, Poisson firing rates or a general Hill-rate sensory model (the injury encoder only adapts its normalized shape), adaptation constants or reported physiological validation. These tests establish distinct wiring and deterministic execution, not neural understanding, faithful biology, reliable walking, swimming or immunity to poisons. Native acceptance remains in [manual-game-test.md](manual-game-test.md).

The adapter now supplies combined ambient/local light to the same Light channel; synthetic table cases above directly specify Light and are unchanged by this adapter addition. Lamp sensor validation and approximation details are in [local-light-sensing.md](local-light-sensing.md).

## Food and escape follow-up

Stock Pumpkin identity comes from `SerialiseInstructions.OriginalSpawnableAsset == ModAPI.FindSpawnable("Pumpkin")`. Nearby strength is max(0, 1 - nearest collider distance / configured vision radius); it is a gameplay proximity convention, including through walls, not a measured odor field. It drives exact `type:ORN_DM1` (74), `type:ORN_DM2` (54), `type:ORN_VA2` (83) without directional bias. Actual native connected-head collision with that stock item drives `type:LB3b` (11) and `type:LB3c` (23) at amplitude 1. Both pass through the existing manual/live boundary and recurrent graph. There is no food-to-motor bypass, eating, nutrition, hunger, swallowing or healing. No other catalog foods are currently allowlisted. Missing stock asset/head, unknown identity, self, disabled/trigger/disintegrated objects provide no food input.

An accepted DNp01 event now starts a 0.6-game-second walking burst, magnitude 0.7 before existing smoothing and native adapter limits. Existing neural backward intent is retained; otherwise the native forward convention is used. The damage sample does not localize the shooter. This can request movement from idle but does not guarantee retreat, a coordinated gait or a fly-like escape. Halt/brake, terminal/invalid state and loss of consciousness cancel it. Real elapsed time expires it across long callbacks. Sensor evidence without actual DNp01 firing cannot start it.

Explicit dismemberment/disintegration of a previously sampled live connected limb emits its last measured health fraction once through the injury-event route. Unknown circulation or already-missing limbs do not invent an event. Dead/invalid person state clears it. Deleting a component between samples may still leave no readable transition.

See [current checks and limitations](food-escape-blood.md).

Object-audio follow-up: the bounded nearby scan includes the native jukebox music source and attached AudioSources, beyond MainAudioSource. Up to 64 unique sources are evaluated; partial scans are labeled. Playback/mute/ownership filters and strongest-source spectrum/direction remain. This is a playback proxy, not sound pressure or music understanding. See [checks](food-escape-blood.md).
