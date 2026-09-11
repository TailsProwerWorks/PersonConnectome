# People Playground API compatibility

`Mod/script.cs` deliberately contains the only compile-time People Playground and Unity references. Its registration uses the standard script-mod pattern: `ModAPI.FindSpawnable("Human")`, `ModAPI.Register(new Modification { ... })`, and `Modification.AfterSpawn` to attach a `MonoBehaviour` marker to the variation's `GameObject`.

Before release, verify those exact symbols and the Human spawnable name against the installed People Playground build. A successful offline .NET build does **not** compile this game script, because the game provides `ModAPI`, `Modification`, and `UnityEngine`.

The marker is intentionally inert: no `Update`, no reflection, no method invocation, no persistence, and no motor action. The separate `src/` adapter is a pure C# reference implementation for a future reviewed game binding. Its public-member candidate names must be checked individually in-game. If a member is absent, private, non-numeric, non-boolean, non-vector, or has a different scale, leave that channel at zero rather than guessing.

The release must not describe the five-node demo graph as MaleCNS, FlyWire, or a whole biological brain.

`config/person-connectome.v1.json` is retained as the sample-file name for compatibility, but its internal `version` is 2 because persisted simulator state now includes delayed events. Consumers must reject a version they do not explicitly support.
