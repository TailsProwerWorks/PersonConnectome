# Sensory and movement mapping

This is an independently implemented C# adaptation of population choices in the Minecraft project's [SensoryEncoders](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/SensoryEncoders.java), [MotorMap](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/MotorMap.java) and [MotorDecoder](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/MotorDecoder.java). It uses our existing authoritative RuntimeBrain and unchanged pinned MaleCNS asset. The reference's [research notes](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/docs/REFERENCE.md) explain its choices. No Minecraft dependency is required.

## What actually enters the graph

Counts below are resolved from the bundled asset, not invented neuron groups. `input:` names are local indexes over its existing annotations. Except photoreceptors, input groups require sensory superclass membership (`cb_sensory`, `vnc_sensory`, `sensory_ascending` or `sensory_descending`). Feature-level visual inputs target named interneurons and are explicitly engineered encoders.

| Observed game signal | Asset selection | Members | Interpretation and limits |
|---|---|---:|---|
| Global ambient grayscale | `ol_sensory` + class `visual` | 6,091 | Uniform photoreceptor amplitude; no color, UV or spatial retina. |
| Increase/decrease between valid ambient-light samples | `Mi1` / `L2`, `L3` | 1,773 / 1,779 / 1,772 | ON/OFF entry choices adapted from the reference. Global change only; first sample, invalid data and terminal recovery establish a new baseline without a fabricated transition. No dark tonic current or per-column image is added. |
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

All eligible population members can receive input, replacing a first-N cutoff that favored low IDs. Refractory neurons still cannot integrate input. Direction is delta.x/distance in world coordinates, not head-relative anatomical localization: right reduces L drive, left reduces R drive. Missing direction uses equal bilateral drive without claiming localization; unknown-side annotations are not assigned a side. Amplitudes are clamped, finite normalized values, not firing rates. The Brain page reports requested encoder amplitudes and unique input neurons queued this tick; recurrent spikes can occur elsewhere too.

## Injury, contact and visual feature scales

`DamageEvent` is the largest normalized health decrease across connected readable limbs since their previous valid sample, not the total of all wounds. First samples, InitialHealth changes, invalid/disconnected/lost readings, suspension and terminal state clear/re-prime history. A fatal limb transition can emit once while the person remains nonterminal. Sampling can miss injury/healing between reads or damage immediately followed by disconnection.

The reference accumulates accepted Minecraft damage and merges it into tactile drive. Our equivalent uses actual sampled limb health, with `q = (DamageEvent / 0.2)^1.5`, encoder amplitude `min(1, 2*q/(1+q))`. Hill exponent 1.5 and scale 0.2 follow its encoder shape; gain 2 is local to our discrete threshold-1 model. A 20% event reaches threshold, and larger events saturate; smaller events contribute graded potential without guaranteeing a spike. This avoids a lone moderate injury event merely decaying forever below threshold in a quiet graph. It does not measure felt pain, calibrated nociception or adrenaline. Native `Pain` remains a separate game reading.

Known regional floor/contact/held flags contribute amplitude 0.15. Broad impact plus vibration*0.35 is clamped; when regional localization is unavailable, sustained broad touch/contact contributes 0.15 per channel. Broad contact/injury and local input combine by maximum. Human-to-fly region names are engineering analogies, not anatomical homology. Water alone is not contact; whole-body impact/vibration can still stimulate several regions.

Visual geometry uses the nearest visible collider's radius `r = max(bounds.extents.x, bounds.extents.y)`, center distance `d`, and target-minus-anchor native velocity. Size is `2*atan(r/d)`; expansion is positive `2*r*closing/(d*d+r*r)`; sweep is `abs(cross(delta, relativeVelocity))/(d*d)`, converted to degrees or degrees/second. This approximates rigid 2D bounds, omitting target deformation/rotation, rendered silhouettes and camera retinal flow. Finite positive geometry and velocities are required.

Following the reference, LC4 amplitude is `expansion/(expansion+200)`, LPLC2 is Gaussian size tuning centered at 60 degrees with sigma 25 while expanding, and LC11/LC18 use `sweep/(sweep+100)` for small moving targets. All are multiplied by existing visibility. The VS proxy uses `abs(nativeAngularSpeed)/(abs(nativeAngularSpeed)+300) * light`; it is unsigned and gated by valid light/rotation. These are deterministic feature encoders, not biological-rate measurements.

## Deliberate unmapped readings

Apart from the new sampled health-drop event above, health, pain, shock, oxygen, consciousness, blood, wounds, brain injury, zombie state and internal liquid identities remain native/derived telemetry and existing control constraints or restorative adjustments. They are not injected into photoreceptors, descending walking neurons, taste or smell populations. Their separately measured mechanical or thermal consequences can still reach the corresponding encoders. A circulating syringe chemical does not establish a fly's external taste stimulus. Wetness/submersion is not ambient humidity; no unsupported hygro channel is synthesized. Velocity XY is telemetry, not wind. No semantic object vision, smell, feeding, flight, courtship or biologically established human grasping is implemented.

The injury-event route is explicitly a mechanical proxy adapted from the reference. It does not restore the older unrelated visual/descending hazard/liquid stimulation. The mod senses more state than it has defensible neural encoders for. An unmapped reading is still displayed; it is not proof that the brain understands that state.

## Movement readout

The reference walking weights are DNp09 0.30, DNg100 0.25, DNge053 0.15, DNge050 0.15 and DNg97 0.15. Our input to the decoder is fired/member fraction, not the reference's Hz readout. Forward, backward and side-specific leg channels use exponential smoothing with a 150 ms time constant. Turning combines DNa02 R-L + 0.5*DNg13 R-L + 0.25*DNa01 R-L with a 100 ms filter. It supplies head/core joint proxies in this 2D body, not facing rotation or flight steering.

Forward mode enters at filtered 0.08 and exits below 0.04, with a 250 ms minimum dwell. Raw MDN fraction >=0.5 immediately selects backward over forward; backward exits below filtered 0.25 after its dwell. Active modes use an absolute 0.3 walk-request floor before the existing rate limiter. At default adapter gain 2, a settled 0.3 submits 0.6 and can clear the native walking gate of 0.5. This is an actuator conversion, not an increase in reported neural activity. Mode and filtered activity are displayed separately. Short quiet gaps are bridged, but sustained silence stops walking.

Raw DNg60/DNg74_a/DNg74_b or AN19A018 fraction >=0.2 clears movement immediately, as do unsafe/terminal control states. These thresholds and the floor are local engineering choices. Fly leg motor subclasses `fl`, `ml`, `hl` contribute side-specific joint proxies (381 total); wing/song/proboscis populations are excluded. MN9 does not request human grips.

The normal request rate limit is eight normalized units per elapsed game second, with elapsed time capped at 0.25 seconds. There is no added oscillator, sensor-only escape direction or force application. Actual walking depends on native pose selection, torque, grounding and sustained requests. Native standing mechanics and existing sensory-derived restorative adjustments remain; exclusive neural control of every game action is not claimed.

## Measured connection checks, not biological validation

Reproduce with `dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release -- --mapping-report`. Each case starts a fresh real graph and runs 40 measured ticks at 0.05 game seconds with the scenario-specific synthetic frame (normally amplitude 1). Injury cases submit one pulse, then zero; geometry/rotation cases use explicit units. ON/OFF cases first prime the preceding valid light sample. These are synthetic sensor frames against the shipped graph, not live game trials.

| Case | Input-population spikes | Whole-graph spikes | Dropped work | Peak absolute walk request |
|---|---:|---:|---:|---:|
| Quiet | 0 | 0 | 0 | 0.000 |
| Constant light | 42,637 | 42,637 | 0 | 0.000 |
| Light increase (Mi1) | 1,773 | 93,198 | 860,358 | 0.300 |
| Light decrease (L2 counted) | 961 | 42,059 | 644,943 | 0.300 |
| Left audio | 434 | 43,882 | 729,951 | 0.300 |
| Right audio | 364 | 41,543 | 757,654 | 0.300 |
| Impact (tactile counted) | 17,906 | 89,456 | 821,076 | 0.300 |
| 50% injury pulse (head counted) | 863 | 59,507 | 824,160 | 0.300 |
| 100% injury pulse (head counted) | 863 | 59,507 | 824,160 | 0.300 |
| Head contact | 2,276 | 43,936 | 647,155 | 0.300 |
| Arm contact | 738 | 29,703 | 499,487 | 0.000 |
| Leg contact | 3,240 | 46,310 | 591,105 | 0.300 |
| Core contact | 898 | 36,039 | 514,218 | 0.000 |
| Loom (LPLC2 counted) | 1,294 | 57,592 | 797,869 | 0.300 |
| Small moving (LC11 counted) | 712 | 34,059 | 658,640 | 0.300 |
| Roll | 160 | 67,732 | 736,646 | 0.300 |
| Low-band audio | 683 | 45,871 | 805,048 | 0.300 |
| High-band audio | 798 | 46,372 | 746,658 | 0.300 |
| Warm | 49 | 53,599 | 848,802 | 0.300 |
| Cool | 49 | 48,559 | 769,621 | 0.300 |
| Joint motion | 2,971 | 55,081 | 845,324 | 0.300 |
| Left tilt | 1,589 | 59,474 | 823,945 | 0.300 |
| Right tilt | 1,729 | 45,431 | 758,725 | 0.300 |
| Left approach fallback (LC4 counted) | 463 | 48,320 | 817,307 | 0.300 |
| Right approach fallback (LC4 counted) | 392 | 57,791 | 947,094 | 0.300 |

Totals include repeated spikes/work across ticks, not unique neurons. Left/right populations differ in membership and wiring; equal raw counts are not expected. The input counts can include recurrent firing of those same neurons. Constant photoreceptor drive alone produced no downstream spikes in this silent-start test; the photoreceptors have outgoing edges, so that is not evidence of disconnected data. Inhibitory input does not initiate excitation in a silent network. The reference supplies additional lamina tonic and spatial visual processing that this runtime does not reproduce.

Strong sustained stimuli still overload the 24,000-neuron tick budget. Dropped work changes neural propagation; bounded memory/work is not equivalence to an unlimited simulation, nor a guaranteed CPU frame time. The model still uses discrete decay, five intervening refractory ticks and one-tick transmission delay at a default 20 Hz control rate. It does not implement the reference's 0.5 ms integrator, Poisson firing rates or a general Hill-rate sensory model (the injury encoder only adapts its normalized shape), adaptation constants or reported physiological validation. These tests establish distinct wiring and deterministic execution, not neural understanding, faithful biology, reliable walking, swimming or immunity to poisons. Native acceptance remains in [manual-game-test.md](manual-game-test.md).
