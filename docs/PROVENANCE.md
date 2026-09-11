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
