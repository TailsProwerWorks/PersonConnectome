# Manual People Playground test checklist

Use a disposable game install. The offline test suite does not have People Playground assemblies, so this is required before claiming an in-game verification.

1. Copy `Mod/` to `Mods/PersonConnectome`; start the game and confirm `mod.json` detects and compiles `script.cs` without error.
2. Spawn **Person Connectome (Active)** from **Entities**. Confirm it is a Human variation and that a stock Human remains unmodified.
3. Press **F7**; verify the overlay says `ACTIVE CONTROL`. Press **F8** before any other test; verify motion stops, the overlay says emergency disabled, and removing/re-spawning is required to re-arm.
4. In a clear area, re-spawn it and verify locomotion and limb motion occur from `DesiredWalkingDirection`/`InfluenceMotorSpeed`. Toggle `ActiveControl` off in the component inspector and verify no subsequent motor mutation. Re-enable it and verify active control resumes.
5. Test nearby object direction (place objects left/right), light/darkness, collision/touch, motion/vibration, and playing physical-object sound. Verify F7 values stay finite and no missing optional feature produces an exception.
6. Independently test health/damage/pain, bleeding/blood loss, limb break/dismemberment/joint stress, fire/temperature, electric charge/shock, water/drowning/oxygen, unconsciousness, infection/zombie, and liquid identities (blood, acid, poison, anesthetic, regeneration/adrenaline when available). Record target-build member/scale differences in `docs/api-compatibility.md`.
7. Test grabbing near a grippable object. If the current build rejects the optional reflected `GripBehaviour.Use` invocation, confirm walking/limb movement continues and document the failure; do not replace it with force or object spawning.
8. With `EnableChemicalOutputs` enabled by default, confirm bounded healing/regeneration, stimulation/adrenaline, and extinguishing only write when the public member is available. Set it false to verify the explicit diagnostic opt-out. Do not add destructive chemistry writes.
9. Simulate slow frames and confirm at most four control ticks are caught up. Check the game log for only clear compatibility notices, not repeated exceptions.

Do not claim successful in-game verification until every applicable step has actually been run on the target build.
