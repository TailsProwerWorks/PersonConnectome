# Sensory and movement mapping

This is an independently implemented C# adaptation of population choices in the Minecraft project's [SensoryEncoders](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/SensoryEncoders.java), [MotorMap](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/MotorMap.java) and [MotorDecoder](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/MotorDecoder.java). It uses our existing authoritative RuntimeBrain and unchanged pinned MaleCNS asset. The reference's [research notes](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/docs/REFERENCE.md) explain its choices. No Minecraft dependency is required.

## What actually enters the graph

Counts below are resolved from the bundled asset, not invented neuron groups. `input:` names are local indexes over its existing annotations. Except photoreceptors, input groups require sensory superclass membership (`cb_sensory`, `vnc_sensory`, `sensory_ascending` or `sensory_descending`). Feature-level visual inputs target named interneurons and are explicitly engineered encoders.

| Observed game signal | Asset selection | Members | Interpretation and limits |
|---|---|---:|---|
| Global ambient grayscale | `ol_sensory` + class `visual` | 6,091 | Uniform photoreceptor amplitude; no color, UV or spatial retina. |
| Increase/decrease between valid ambient-light samples | `Mi1` / `L2`, `L3` | 1,773 / 1,779 / 1,772 | ON/OFF entry choices adapted from the reference. Global change only; first sample, invalid data and terminal recovery establish a new baseline without a fabricated transition. No dark tonic current or per-column image is added. |
| Strongest accepted external playback | class `mechanosensory`, subclass `auditory` | 114 | Normalized volume/distance proxy; world-horizontal bearing weights annotated L/R sides. No song/frequency/object understanding. |
| Contact impact and weaker sustained contact/vibration | class `mechanosensory_tactile` | 2,558 | Whole-body tactile proxy. No pain-receptor identity or specific fly body-region localization is inferred. Foot-floor contact may be self-generated. |
| Native signed body tilt | class `mechanosensory`, subclass `wind_gravity` | 475 | Engineered body-to-antenna gravity proxy, not measured wind. |
| Connected hinge angle magnitude | proprioceptive class, subclass `hair plate` | 113 | Peak valid angle/180, not a reconstructed fly joint. |
| Connected hinge speed magnitude | proprioceptive class, subclass `chordotonal organ` | 425 | Peak valid speed/180, not a calibrated chordotonal tuning curve. |
| Connected hinge stress | proprioceptive class, subclass `campaniform sensilla` | 426 | Peak valid stress/100. Invalid or disconnected joints cannot contribute through another valid limb. Whole-body injury stress remains separate telemetry. |
| Native body/local/proximity-derived heat | thermosensory class, `TRN_VP2` | 7 | Warm channel; game thresholds are engineering scales. |
| Native body/local/proximity-derived cold | thermosensory class, `TRN_VP3a` or `TRN_VP3b` | 7 | Cool channel; no claim of adult nociceptive calibration. |
| Visible target with positive radial closing velocity | `LC4`, `LPLC2` | 126 / 185 | Analytic approach feature, attenuated by visibility/proximity. It is not measured angular expansion or semantic vision. Mere proximity or a receding target does not drive it. |

Thermal choices are supported by [adult thermo/hygrosensory connectomics](https://pmc.ncbi.nlm.nih.gov/articles/PMC7443704/). In particular, a `TRN` prefix alone is insufficient: VP1m participates in hygrosensation and is not included as a warm/cool receptor. Auditory and gravity modalities are supported by [Johnston's-organ auditory analysis](https://pmc.ncbi.nlm.nih.gov/articles/PMC4023023/) and [wind/gravity sensory analysis](https://pmc.ncbi.nlm.nih.gov/articles/PMC2755041/); these sources do not validate a human game body's input scaling.

All eligible population members can receive input, replacing a first-N cutoff that favored low IDs. Refractory neurons still cannot integrate input. Direction is delta.x/distance in world coordinates, not head-relative anatomical localization: right reduces L drive, left reduces R drive. Missing direction uses equal bilateral drive without claiming localization; unknown-side annotations are not assigned a side. Amplitudes are clamped, finite normalized values, not firing rates. The Brain page reports requested encoder amplitudes and unique input neurons queued this tick; recurrent spikes can occur elsewhere too.

## Deliberate unmapped readings

Health, pain, shock, oxygen, consciousness, blood, wounds, brain injury, zombie state and internal liquid identities remain native/derived telemetry and existing control constraints or restorative adjustments. They are not injected into photoreceptors, descending walking neurons, taste or smell populations. Their separately measured mechanical or thermal consequences can still reach the corresponding encoders. A circulating syringe chemical does not establish a fly's external taste stimulus. Wetness/submersion is not ambient humidity; no unsupported hygro channel is synthesized. Velocity XY is telemetry, not wind. No semantic object vision, smell, feeding, flight, courtship or biologically established human grasping is implemented.

This intentionally supersedes the earlier broad injury/hazard/liquid neural route. The mod senses more state than it has defensible neural encoders for. An unmapped reading is still displayed; it is not proof that the brain understands that state.

## Movement readout

The reference walking population weights are DNp09 0.30, DNg100 0.25, DNge053 0.15, DNge050 0.15 and DNg97 0.15. Our decoder applies them to latest-tick fired/member fractions. MDN activity at fraction >=0.2 takes backward priority; DNg60/DNg74_a/DNg74_b or AN19A018 activity at fraction >=0.2 clears movement requests immediately. These fraction thresholds are local engineering choices, not the reference's Hz thresholds. Fly leg motor subclasses `fl`, `ml`, `hl` contribute side-specific joint proxies (381 total); wing/song/proboscis populations are excluded. MN9 extends a fly proboscis and no longer requests human grips.

The existing human arm/leg/core conversion, normal request rate limiter and local native actuator checks remain. There is no added oscillator, sensor-only escape direction or force application. Actual walking still depends on native pose selection, torque, grounding and the request lasting long enough to move the body. Native standing mechanics and existing sensory-derived restorative adjustments remain, so exclusive neural control of every game action is not claimed.

## Measured connection checks, not biological validation

Reproduce with `dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release -- --mapping-report`. Each case starts a fresh real graph and runs 40 measured ticks at 0.05 game seconds with amplitude 1. ON/OFF cases first prime the preceding valid light sample. These are synthetic sensor frames against the shipped graph, not live game trials.

| Case | Input-population spikes | Whole-graph spikes | Dropped work | Peak absolute walk request |
|---|---:|---:|---:|---:|
| Quiet | 0 | 0 | 0 | 0.000 |
| Constant light | 42,637 | 42,637 | 0 | 0.000 |
| Light increase (Mi1) | 1,773 | 93,198 | 860,358 | 0.500 |
| Light decrease (L2 only counted here) | 961 | 42,059 | 644,943 | 0.400 |
| Left audio | 434 | 43,882 | 729,951 | 0.650 |
| Right audio | 364 | 41,543 | 757,654 | 0.400 |
| Impact | 17,906 | 83,003 | 758,173 | 0.300 |
| Warm | 49 | 53,599 | 848,802 | 0.400 |
| Cool | 49 | 48,559 | 769,621 | 0.275 |
| Joint motion | 2,971 | 55,081 | 845,324 | 0.400 |
| Left tilt | 1,589 | 59,474 | 823,945 | 0.400 |
| Right tilt | 1,729 | 45,431 | 758,725 | 0.400 |
| Left approach (LC4 only counted here) | 463 | 48,320 | 817,307 | 0.250 |
| Right approach (LC4 only counted here) | 392 | 57,791 | 947,094 | 0.400 |

Totals include repeated spikes/work across ticks, not unique neurons. Left/right populations differ in membership and wiring; equal raw counts are not expected. The input counts can include recurrent firing of those same neurons. Constant photoreceptor drive alone produced no downstream spikes in this silent-start test; the photoreceptors have outgoing edges, so that is not evidence of disconnected data. Inhibitory input does not initiate excitation in a silent network. The reference supplies additional lamina tonic and spatial visual processing that this runtime does not reproduce.

Strong sustained stimuli still overload the 24,000-neuron tick budget. Dropped work changes neural propagation; bounded memory/work is not equivalence to an unlimited simulation, nor a guaranteed CPU frame time. The model still uses discrete decay, five intervening refractory ticks and one-tick transmission delay at a default 20 Hz control rate. It does not implement the reference's 0.5 ms integrator, Poisson/Hill encoder rates, adaptation constants or reported physiological validation. These tests establish distinct wiring and deterministic execution, not neural understanding, faithful biology, reliable walking, swimming or immunity to poisons. Native acceptance remains in [manual-game-test.md](manual-game-test.md).
