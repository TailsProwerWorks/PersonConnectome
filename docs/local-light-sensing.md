# Local light sensing - 2026-09-14

Previously the adapter read only RenderSettings.ambientLight.grayscale. A nearby cathode light or flashlight therefore could not change the light channel unless map ambient brightness also changed.

## Supported native sources

Within the existing nearby-collider search, each external PhysicalBehaviour owner is inspected once. The adapter uses explicit public references confirmed in installed People Playground 1.27.17 assembly metadata/IL:

- LightSprite children: SpriteRenderer and Brightness. Radius changes transform scale in native code; it is not treated as a metric light radius.
- GlowtubeBehaviour.LightSprite, BulbBehaviour.LightSprite and LEDBulbBehaviour.LightSprite: current renderer state/color. Native gas/power/filament effects remain the game's responsibility.
- ActivationToggleBehaviour.LightObject and SingleFloodlightBehaviour.ToToggle: renderers beneath the native toggled light group.
- FlashlightAttachmentBehaviour.Lights: renderer enabled state also reflects EMP/disconnection shutdown.

Current renderer enabled/active state is authoritative for this proxy. An activation flag alone is insufficient: broken bulbs may be disabled and cooling filaments may remain luminous. Arbitrary emissive body sprites, visual UI labels and unrecognized particles/materials are not invented light sources. Self-owned objects follow the existing nearby-source exclusion. Sources without a nearby external PhysicalBehaviour, including some map-only lights, are not sampled.

## Estimate and data path

The sample position is the connected brain limb's anchor, or the existing root fallback. It is transformed into each light renderer's local coordinates. Sprite bounds define a footprint; flips, world scale, rotation and sliced/tiled renderer size are honored. Zero/nonfinite dimensions or scale, missing sprites, invalid color/brightness, disabled/inactive sources and unsupported draw modes cannot contribute.

Each footprint uses elliptical radial falloff: strength times max(0, 1 - normalizedRadius). Strength is clamped renderer grayscale times alpha, with the native Brightness factor for LightSprite components. For other native owner references, color/alpha is the strength proxy; arbitrary shader/material overrides are not reconstructed. The strongest contribution is local-light-proxy; duplicate references/colliders cannot amplify it. Total Light is clamp01(ambient + local proxy). Invalid ambient data keeps the combined channel invalid, preserving the existing transition-baseline rules.

The total feeds the existing photoreceptor channel, light-level ON/OFF encoders and visual-proxy attenuation. It is not display-only. Manual input still overrides the existing channel according to Mixed/Manual-only settings. Full brightness saturates at 1, so an additional lamp need not increase an already saturated input. No lamp identity, color perception or semantic recognition is assigned to neurons.

The search keeps the existing 128-collider/radius bound (8 world units by default) and samples at most 64 distinct eligible light renderers per person per read. Filled collider or light limits display light-scan=partial. Component discovery reuses lists and owner/renderer sets; it adds no whole-scene scan. These bounds do not guarantee a frame-time limit for custom objects with huge component hierarchies.

## Important approximation

There is no exposed CPU-side final illumination-at-world-point API in the inspected game surface. Native lighting uses sprite textures, materials and a camera/render-texture/postprocessing pipeline. This implementation does not read pixels, integrate texture alpha profiles, reproduce exact flashlight cones/shadows/occlusion, or infer native effective shader intensity for arbitrary materials. A footprint contribution is an estimate, not proof that the sampled world pixel is visibly lit, lux, a retinal image or biological vision.

The UI shows ambient-light separately from local-light-proxy and contributing-light-sprites. The overall Light and ON/OFF pathways use this documented combined scalar. The screen overlay itself remains independent of world lighting.

## Validation and files

Regression coverage includes seven native owner routes, off/inactive/transparent sources, duplicate routes/colliders, ordinary glowing-sprite exclusion, transformed/flipped footprints, invalid/missing data, zero scale, sliced/tiled sizes, native LightSprite brightness and 64-source truncation/reset. The first geometry fixture used the wrong test anchor and was corrected to use the adapter's root fallback.

Mod implementation changes: src/Adapters/PeoplePlaygroundPersonAdapter.cs, Core/SensoryFrame.cs and Core/MotorCommand.cs, Core/LifBrain.cs (label), UI/ManualInputState.cs (label), UI/StatusDisplay.cs (explanation). Adapter tests change Program.cs and GameDoubles.cs. README.md, assets/README.txt, architecture/API/provenance/sensory-mapping/Minecraft-adaptation/manual-game-test docs and this file describe the new behavior.

Native rendering acceptance remains manual-game-test.md case 31. Compiling against installed assemblies and passing game doubles do not prove every stock prefab's beam footprint or native performance. Restart/reload the mod before checking the result; an old processed=24000/24000 panel also indicates the earlier scheduler is still loaded.

Checks completed on 2026-09-14:

| Command/check | Result |
| --- | --- |
| `dotnet format PersonConnectome.slnx --verify-no-changes --no-restore` | Passed, exit 0 |
| `dotnet build PersonConnectome.slnx -c Release` | Passed, 0 warnings and 0 errors |
| `dotnet run --project tests/PersonConnectome.Runtime.Tests -c Release` | Passed, 53 scenarios |
| `dotnet run --project tests/PersonConnectome.Adapter.Tests -c Release` | Passed, 61 scenarios |
| `dotnet build src/PersonConnectome.Mod.csproj -c Release` | Passed, 0 warnings and 0 errors |
| `scripts/ai/Test-GameCompilation.ps1` | Passed: 12 scripts, 22 exact references, syntax checks and both installed semantic scanners |
| PowerShell parser over `scripts/**/*.ps1` | Passed, 6 scripts |
| `scripts/ai/Test-DeployDiscovery.ps1` | Passed, including UTF-8/5000-byte README boundary coverage |
| `scripts/deploy/Deploy-Mod.ps1 -WhatIf` | Passed, registered Steam discovery and game-install forwarding |
| `git diff --check` | Passed |

Local deployment used `scripts/deploy/Deploy-Mod.ps1 -GameInstall <discovered install> -NoBuild` after validation. Source and installed Workshop creator identities matched before deployment. All 16 deployed files matched source hashes, including the unchanged connectome carrier; the raw build input was not deployed. This did not upload a Workshop update. The embedded marker is the existing HEAD `4dfaaef7312b`; these working-tree changes are not a new commit. No native gameplay or rendering acceptance run was performed.
