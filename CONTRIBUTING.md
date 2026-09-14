# Contributing to Person Connectome

Thanks for helping improve Person Connectome. This is a People Playground mod that runs a bounded, thresholded MaleCNS-derived fly connectome and adapts its signals to a game Human. Contributions should preserve that distinction: the neural graph is real project data and the runtime activity is real simulation output, but many sensor scales and Human actuator mappings are engineering adaptations.

## Read this first

Before changing code, read:

- [README](README.md) for the user-facing behavior and known limits.
- [Architecture](docs/architecture.md) for the runtime boundaries and control loop.
- [Sensory and movement mapping](docs/sensory-mapping.md) for every supported route and deliberate non-mapping.
- [API compatibility](docs/api-compatibility.md) for inspected People Playground fields and methods.
- [Manual game checks](docs/manual-game-test.md) for native rendering, physics, UI and performance validation.
- [Provenance](docs/PROVENANCE.md) and [third-party notices](THIRD_PARTY_NOTICES) before changing the connectome asset or attribution.

`Mod/RuntimeBrain.cs` is the authoritative brain implementation. It is compiled into the game mod and linked by the runtime test harness; do not create a second decoder or silently change only one copy.

## Model assistance

If you do not already understand the repository’s architecture and its biological-versus-engineering boundary, use a strong reasoning model that can read the relevant files together and run the complete validation workflow. The maintainer’s recommended options are:

- GPT-6 Astra or GPT-5.6 Sol
- Claude Fable 5.1 or Claude Opus 5

These are recommendations for complex repository work, not a substitute for reviewing the source, running tests, or checking native People Playground behavior. Ask the model to identify its evidence, preserve unrelated changes, and distinguish offline proof from in-game proof.

## Validation

From the repository root, run the applicable checks:

```powershell
dotnet format PersonConnectome.sln --verify-no-changes --no-restore
dotnet build PersonConnectome.sln -c Release
dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release
dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release
pwsh -NoProfile -File scripts/Test-GameCompilation.ps1
git diff --check
```

For changes to deployment or source safety, also run the relevant scripts under `scripts/`, especially `Test-ModSourceSafety.ps1` and `Test-DeployDiscovery.ps1`. For UI, physics, loading, audio, vision, timing or performance changes, complete the applicable sections of [manual-game-test.md](docs/manual-game-test.md) and report native results separately from offline test results.

## What to include in a change

Describe:

- what behavior changed and why;
- which source route, native API or asset data is involved;
- what regression tests were added or updated;
- the exact validation commands and results;
- whether People Playground was actually launched and tested;
- any remaining approximation, performance or compatibility limitation.

For visual or native behavior changes, include a screenshot or log when practical. Do not claim biological fidelity, effective movement, native rendering correctness or real-time performance from source inspection or headless tests alone.

## Asset and attribution changes

The bundled graph is derived from MaleCNS v1.0 data and carries project-specific provenance. Do not replace, regenerate or rename the carrier casually. If the payload or its transformation changes, update the identity checks, provenance documentation, third-party notices and relevant regression tests together.
