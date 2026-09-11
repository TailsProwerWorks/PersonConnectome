# Architecture

There are two implementations with an explicit boundary:

- `src/PersonConnectome` is the reusable .NET 10 graph/simulator/controller library, including optional reflection capabilities and JSON state. It is not referenced or deployed by the game mod.
- `Mod` is the standalone net48 game-facing source set. `script.cs` owns its sparse brain; the adapter directly samples and commands installed game APIs. Runtime and adapter tests link these exact source files with narrow game/Unity doubles.

The shipped asset is a thresholded MaleCNS v1.0 derivative: 176,422 neurons, 6,287,749 retained connections (weight >=5, self-edges excluded). It is not the full released connection graph. The PNG carries the exact compressed FLYB bytes. Runtime verifies SHA-256, dataset identity, exact counts, CSR structure, target and used metadata indexes before caching shared graph arrays. Each person has independent dynamic neural state.

At each control tick, the adapter reconciles surviving/new limbs and builds a fresh bounded sensory frame. Heuristic sensory populations receive weighted game signals; nearby direction and liquid healing feed the command decoder only. Sparse LIF processing caps work at 24,000 active neurons, rotates overloaded work, merges carried input with new propagation and expires refractory periods by neural tick. Telemetry exposes queued input targets, processed/deferred work and spikes. This bounds neuron work, not a guaranteed wall-clock budget or biological fidelity.

Terminal/invalid frames clear neural state and stop commands. Motor cleanup sets joint influence to one even for incapable limbs, drops grips and restores only owned regeneration boosts. Nonterminal unconsciousness is sampled but motor/chemistry commands are suppressed. Recovery from terminal state starts from cleared state. The scheduler allows at most four catch-up ticks.

The independent overhead label displays requested versus applied commands and follows the current head/root without inheriting ragdoll mirroring. It has no physics/control responsibility. No game persistence is implemented; reusable-library state tests do not establish Unity save compatibility.
