# MaleCNS runtime provenance

The downloaded source payload is `Mod/connectome/malecns-v1.0.flyb.gz`. Because People Playground's Shady Code Rejection rules forbid direct file and binary-reader APIs in mod scripts, the exact compressed bytes are packed into `Mod/connectome/malecns-v1.0.png` and loaded with the documented `ModAPI.LoadTexture` API. The raw payload is retained as a repository/build input; deployment copies only the PNG carrier. The game-facing loader accounts for Unity's bottom-to-top `Texture2D.GetPixels32()` ordering when reconstructing the original byte stream before decompression.

| Field | Value |
|---|---|
| Dataset | `male-cns:v1.0` |
| Source | [MaleCNS download page](https://male-cns.janelia.org/download/) |
| Prepared derivative | [fly-brain-minecraft FLYB asset](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/resources/connectome/malecns-v1.0.flyb.gz) |
| Retrieved | 2026-09-11 |
| Runtime format | gzip-compressed FLYB v1, little-endian CSR |
| Neurons | 176,422 |
| Retained connections | 6,287,749 |
| Threshold | connection synapse weight >= 5 |
| Runtime asset SHA-256 | `E33DF182BED7A6F3EA279DAF4790A82B05706D3D41E819A6A80C0473E8C559F3` |
| License | Male CNS data CC BY 4.0; see `THIRD_PARTY_NOTICES` |

## Transformations represented by the asset

The upstream preparation fetches MaleCNS/neuPrint neuron metadata and weighted
segment-to-segment connections, keeps edges at or above five synapses, removes
self-edges, sorts neurons by body ID, compacts external IDs to contiguous indexes,
and stores outgoing edges as CSR row pointers, target indexes, and 16-bit
synapse counts. Neuron metadata includes type, superclass, class, subclass,
neurotransmitter, side, neuromere, and related annotation tables. The asset
also contains the prepared retina table.

At runtime this repository independently reads the FLYB v1 layout, validates
the expected compressed SHA-256, dataset identity, exact neuron/edge/retina counts,
magic/version, CSR monotonicity, target bounds, and used metadata indexes. Synapse counts are converted to the normalized
LIF current used by this controller with `0.275 mV * 0.65 / 7 mV`; the
prepared file does not contain per-edge delays, so the runtime uses one
simulation-tick delay. Neurotransmitter signs from the asset are applied as
excitatory/inhibitory signs; the model is an engineering approximation, not a
claim that every fly synapse is represented biologically exactly.

## Reproducibility and licensing boundary

The original MaleCNS acquisition requires the source's documented bulk files or
neuPrint access and may require an account/token. Runtime never contacts the
network. Rebuilding the derivative must preserve the MaleCNS CC BY attribution,
the exact source dataset/version, the threshold and transformations above, and
must update the checksum and counts here. FlyWire CC BY-NC data is not mixed into
this asset.

## Diagnostic soma map

The same pinned FLYB payload also contains interleaved float32 soma X/Y/Z coordinates (raw neuPrint voxel coordinates; NaN for missing positions), as documented by the upstream [FLYB builder](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/tools/build_flyb.py). The current payload has 141,781 neurons with three finite coordinates. Runtime preserves a deterministic evenly spaced sample of up to 8,192 of these, retaining original compact neuron IDs and superclass categories. The diagnostic X/Z projection preserves relative coordinates with a common scale and does not invent positions for the remaining neurons. All 176,422 graph neurons remain in the simulation.

White points correspond to sampled IDs in this runtime's actual fired set for the displayed capture. The image is not a human-brain reconstruction, a membrane-voltage measurement or a complete activity recording. Whole-graph spike history and population fired/member bars are separate diagnostics. Their values are not asserted to be biological firing rates.

The [Minecraft brain view](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/client/java/com/fruitfly/client/hud/BrainViewHud.java) inspired the requested presentation. This implementation uses the existing asset and a local Unity display; no Minecraft game mechanics, reflex controller or replacement connectome were imported. The compressed payload and carrier were not changed by this polish.

## Game liquid mapping provenance

The adapter's 41 stock liquid IDs come from static inspection of `Global.Awake` registration calls in the locally installed People Playground 1.27.17 assembly. Selected liquid effect methods were inspected to separate exposure from observed native effects, as recorded in api-compatibility.md. This is game integration metadata, not MaleCNS chemical-sense data. It does not change the connectome, source attribution, payload checksum or PNG carrier.

## Sensory and motor reference adaptation

The current C# encoders adapt named population choices, global luminance ON/OFF entry points, Hill-derived damage response, regional contact and geometric visual feature formulas from [SensoryEncoders.java](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/SensoryEncoders.java). Locomotor weights, filter time constants and mode-hysteresis structure refer to [MotorMap.java](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/MotorMap.java) and [MotorDecoder.java](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/brain/MotorDecoder.java). Reviewed on 2026-09-12. The reference is [MIT licensed](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/LICENSE), copyright 2026 Blendi Remade / fal.ai; the notice is preserved in THIRD_PARTY_NOTICES and shipped README.txt.

[Exact mapping and measured limits](sensory-mapping.md) separates source annotation, primary biological evidence and local engineering projection. New class/subclass/nerve indexes read existing asset columns; no neurons, edges, soma positions, compressed bytes or carrier pixels were changed. Internal liquid identity is game telemetry, not connectome chemical-receptor data. The existing tick integrator is retained; the reference's millisecond dynamics, flight/reflex controllers and validation results are not claimed for this mod.

Native health-drop sampling and bounds/velocity sensing were adapted for People Playground after reviewing the reference [WorldSenses.java](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/entity/WorldSenses.java) and [FlyEntity.java](https://github.com/blendi-remade/fly-brain-minecraft/blob/main/src/main/java/com/fruitfly/entity/FlyEntity.java). The injury gain, human-region analogies, two-dimensional turning projection and walking actuator floor are local engineering choices. Coarse audio bands use Unity source spectrum energy rather than Minecraft sound identifiers. The [coverage matrix](minecraft-adaptation.md) distinguishes these adaptations from unsupported fly actions, chemical senses and simulation dynamics. No scientific equivalence to the reference is asserted.
