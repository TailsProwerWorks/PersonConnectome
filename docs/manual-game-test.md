# Manual People Playground checklist

1. In a disposable game install, copy `Mod/` to the game's `Mods/PersonConnectome` directory; confirm the mod list detects `mod.json` and compiles `script.cs` with no error.
2. Spawn **Person Connectome Observer** (not the stock Human) and confirm it is a normal Human with only the inert marker attached. Confirm loading/spawning neither alters, spawns, damages, moves, nor deletes any unrelated object.
3. Confirm `ModAPI.FindSpawnable("Human")`, `ModAPI.Register`, `Modification.AfterSpawn`, and `GameObject.AddComponent` are supported by the exact target build. If any differs, update only the isolated registration block in `Mod/script.cs`; do not add a speculative fallback.
4. On the exact target build, implement a reviewed capability bridge one sensor at a time. Verify a missing property gives zero and no exception.
5. Exercise alive/death, limb/body movement, contact, damage/bleeding, fire/temperature, electricity, drowning, impact/fall, nearby objects/light/LOS and sound only when their target-build APIs are known.
6. Exercise water, blood, acid, poison, anesthetic/knockout, syringe/serum, zombie/infection, regeneration, adrenaline and immortality independently. Confirm channel values remain `[0,1]`; record unknown API/effect names locally rather than guessing.
7. Confirm observe-only mode produces no game action. Test emergency disable before enabling any supported command.
8. If a supported bridge is enabled, test smoothing, finite clamping and maximum tick catch-up at slow frame rates. Do not bind force, damage, spawning, deletion, or persistence without a new review.
